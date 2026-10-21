using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BrandWorkspace.Api;
using BrandWorkspace.Api.Sso;
using BrandWorkspace.Persistence.Access;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using WoW.Two.Sdk.Beta.Be.Testing;
using Xunit;

namespace BrandWorkspace.Tests.E2E.Access;

/// <summary>Verifies real HTTP, cookie authority, and PostgreSQL delegation behavior without live credentials.</summary>
public sealed class SsoHttpContractTests(AccessDatabaseFixture database) : IClassFixture<AccessDatabaseFixture>
{
    private const string Secret = "fixture-only-sso-client-secret-long-enough";
    private const string Redirect = "https://catalogue.example.test/api/sso/callback";
    private static readonly string Verifier = new('v', 64);
    private static readonly string State = new('s', 64);

    [Fact]
    public async Task Authorize_ShouldResumeAtDeskLoginWithoutAcceptingExternalRedirects()
    {
        await using var factory = Factory(Guid.NewGuid());
        using var client = Client(factory);
        using var response = await client.GetAsync(AuthorizeUrl());
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/?returnUrl=%2Fapi%2Fsso%2Fauthorize", response.Headers.Location!.OriginalString);
        Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
        using var hostile = await client.GetAsync(AuthorizeUrl("https://evil.test/api/sso/callback"));
        Assert.Equal(HttpStatusCode.BadRequest, hostile.StatusCode);
        Assert.Null(hostile.Headers.Location);
        Assert.Equal("application/problem+json", hostile.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task DisabledRegistry_ShouldRejectEvenPreviouslyConfiguredClient()
    {
        await using var factory = Factory(Guid.NewGuid(), false);
        using var client = Client(factory);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(AuthorizeUrl())).StatusCode);
    }

    [Fact]
    public async Task Exchange_ShouldBindVerifierClientRedirectAndPreventReplay()
    {
        var organization = Guid.NewGuid();
        await using var factory = Factory(organization);
        using var browser = Client(factory);
        await SignInMemberAsync(factory, browser, organization);
        var code = await CodeAsync(browser);
        using var server = Client(factory, false);
        using var badVerifier = await ExchangeAsync(server, code, new string('q', 64));
        Assert.Equal(HttpStatusCode.BadRequest, badVerifier.StatusCode);
        using var wrongSecret = await server.PostAsJsonAsync("/api/sso/exchange", new { clientId = "catalogue", clientSecret = "wrong", redirectUri = Redirect, code, codeVerifier = Verifier });
        Assert.Equal(HttpStatusCode.BadRequest, wrongSecret.StatusCode);
        using var wrongRedirect = await server.PostAsJsonAsync("/api/sso/exchange", new { clientId = "catalogue", clientSecret = Secret, redirectUri = "https://elsewhere.test/api/sso/callback", code, codeVerifier = Verifier });
        Assert.Equal(HttpStatusCode.BadRequest, wrongRedirect.StatusCode);
        using var exchanged = await ExchangeAsync(server, code);
        Assert.Equal(HttpStatusCode.OK, exchanged.StatusCode);
        Assert.True(exchanged.Headers.CacheControl!.NoStore);
        Assert.Empty(exchanged.Headers.TryGetValues("Set-Cookie", out var cookies) ? cookies : []);
        var grant = (await exchanged.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.True(grant.GetProperty("sessionExpiresAt").GetDateTimeOffset() > grant.GetProperty("expiresAt").GetDateTimeOffset());
        Assert.True(await ActiveAsync(server, grant.GetProperty("ticket").GetString()!));
        Assert.Equal(HttpStatusCode.BadRequest, (await ExchangeAsync(server, code)).StatusCode);
    }

    [Fact]
    public async Task ExchangedTicket_ShouldIntrospectTheOperatorsCurrentDeskProfile()
    {
        var organization = Guid.NewGuid();
        await using var factory = Factory(organization);
        using var browser = Client(factory);
        var (_, user) = await SignInMemberAsync(factory, browser, organization);
        using var server = Client(factory, false);
        var grant = await TokenAsync(await ExchangeAsync(server, await CodeAsync(browser)));
        Assert.False(grant.TryGetProperty("email", out _));
        var ticket = grant.GetProperty("ticket").GetString()!;
        var actor = await IntrospectAsync(server, ticket);
        Assert.True(actor.GetProperty("active").GetBoolean());
        Assert.Equal(organization, actor.GetProperty("organizationId").GetGuid());
        Assert.Equal("Test operator", actor.GetProperty("displayName").GetString());
        Assert.Equal(user.Email, actor.GetProperty("email").GetString());
        await database.ExecuteAsync("UPDATE access.users SET \"DisplayName\"='' WHERE id=$1", user.Id);
        Assert.Equal(user.Email, (await IntrospectAsync(server, ticket)).GetProperty("displayName").GetString());
        await database.ExecuteAsync("DELETE FROM access.memberships WHERE organization_id=$1", organization);
        var revoked = await IntrospectAsync(server, ticket);
        Assert.False(revoked.GetProperty("active").GetBoolean());
        Assert.False(revoked.TryGetProperty("email", out _));
    }

    [Fact]
    public async Task Backchannel_ShouldRejectBrowserOriginAndPlaintextTransport()
    {
        var organization = Guid.NewGuid();
        await using var factory = Factory(organization);
        using var browser = Client(factory);
        await SignInMemberAsync(factory, browser, organization);
        var code = await CodeAsync(browser);
        using var server = Client(factory, false);
        server.DefaultRequestHeaders.Add("Origin", "https://localhost");
        Assert.Equal(HttpStatusCode.BadRequest, (await ExchangeAsync(server, code)).StatusCode);
        server.DefaultRequestHeaders.Remove("Origin");
        server.DefaultRequestHeaders.Add("Sec-Fetch-Site", "same-origin");
        Assert.Equal(HttpStatusCode.BadRequest, (await ExchangeAsync(server, code)).StatusCode);
        using var insecure = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("http://localhost"), AllowAutoRedirect = false });
        Assert.Equal(HttpStatusCode.BadRequest, (await ExchangeAsync(insecure, code)).StatusCode);
        server.DefaultRequestHeaders.Remove("Sec-Fetch-Site");
        Assert.Equal(HttpStatusCode.OK, (await ExchangeAsync(server, code)).StatusCode);
    }

    [Fact]
    public async Task Refresh_ShouldRotateOnceAndGlobalLogoutShouldInvalidateAllDelegatedAccess()
    {
        var organization = Guid.NewGuid();
        await using var factory = Factory(organization);
        using var browser = Client(factory);
        var (csrf, _) = await SignInMemberAsync(factory, browser, organization);
        using var server = Client(factory, false);
        var issued = await TokenAsync(await ExchangeAsync(server, await CodeAsync(browser)));
        var old = issued.GetProperty("refreshToken").GetString()!;
        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => RefreshAsync(server, old)));
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Equal(3, responses.Count(response => response.StatusCode == HttpStatusCode.BadRequest));
        var rotated = await TokenAsync(responses.Single(response => response.StatusCode == HttpStatusCode.OK));
        Assert.Equal(issued.GetProperty("sessionExpiresAt").GetDateTimeOffset(), rotated.GetProperty("sessionExpiresAt").GetDateTimeOffset());
        using var logout = await AccessDatabaseFixture.PostAsync(browser, "/api/auth/logout", new { }, csrf);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.False(await ActiveAsync(server, rotated.GetProperty("ticket").GetString()!));
        Assert.Equal(HttpStatusCode.BadRequest, (await RefreshAsync(server, rotated.GetProperty("refreshToken").GetString()!)).StatusCode);
        foreach (var response in responses) response.Dispose();
    }

    [Fact]
    public async Task MembershipRemoval_ShouldBlockTicketIntrospectionAndRefresh()
    {
        var organization = Guid.NewGuid();
        await using var factory = Factory(organization);
        using var browser = Client(factory);
        await SignInMemberAsync(factory, browser, organization);
        using var server = Client(factory, false);
        var issued = await TokenAsync(await ExchangeAsync(server, await CodeAsync(browser)));
        await database.ExecuteAsync("DELETE FROM access.memberships WHERE organization_id=$1", organization);
        Assert.False(await ActiveAsync(server, issued.GetProperty("ticket").GetString()!));
        Assert.Equal(HttpStatusCode.BadRequest, (await RefreshAsync(server, issued.GetProperty("refreshToken").GetString()!)).StatusCode);
    }

    [Fact]
    public async Task ConcurrentCodeExchange_ShouldProduceOneDurableGrant()
    {
        var organization = Guid.NewGuid();
        await using var factory = Factory(organization);
        using var browser = Client(factory);
        await SignInMemberAsync(factory, browser, organization);
        var code = await CodeAsync(browser);
        using var server = Client(factory, false);
        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => ExchangeAsync(server, code)));
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Equal(3, responses.Count(response => response.StatusCode == HttpStatusCode.BadRequest));
        foreach (var response in responses) response.Dispose();
    }

    [Fact]
    public async Task OversizedBody_ShouldReturnProblemBeforeBindingAndNotCache()
    {
        await using var factory = Factory(Guid.NewGuid());
        using var client = Client(factory, false);
        using var body = new StringContent(new string('x', 17000), Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/api/sso/exchange", body);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        Assert.True(response.Headers.CacheControl!.NoStore);
    }

    [Fact]
    public async Task RepeatedInvalidRequests_ShouldBeRateLimitedWithProblemDetails()
    {
        await using var factory = Factory(Guid.NewGuid());
        using var client = Client(factory, false);
        for (var index = 0; index < 30; index++)
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/sso/authorize")).StatusCode);
        using var limited = await client.GetAsync("/api/sso/authorize");
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal("application/problem+json", limited.Content.Headers.ContentType!.MediaType);
        Assert.True(limited.Headers.CacheControl!.NoStore);
        Assert.NotNull(limited.Headers.RetryAfter);
    }

    private WebApiTestHost<ApiAssemblyMarker> Factory(Guid organization, bool enabled = true)
    {
        var factory = new WebApiTestHost<ApiAssemblyMarker>
        {
            ConfigureHostHook = builder => builder.UseEnvironment(Environments.Development),
            ConfigureConfigurationHook = configuration => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Database"] = database.Connection, ["AllowedHosts"] = "localhost;127.0.0.1",
                ["DataProtection:KeysPath"] = database.KeysPath, ["DataProtection:ApplicationName"] = "brand-workspace:sso-tests",
                ["Sso:Enabled"] = enabled.ToString(), ["Sso:Clients:0:Id"] = "catalogue",
                ["Sso:Clients:0:RedirectUri"] = Redirect, ["Sso:Clients:0:Audience"] = "ocharo-catalogue",
                ["Sso:Clients:0:OrganizationId"] = organization.ToString(),
                ["Sso:Clients:0:SecretSha256"] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Secret))),
                ["Integrations:SigningKey"] = "fixture-only-integration-signing-key-32plus"
            })
        };
        factory.Clock.SetUtcNow(DateTimeOffset.UtcNow);
        return factory;
    }

    private static HttpClient Client(WebApiTestHost<ApiAssemblyMarker> factory, bool cookies = true) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = cookies });

    private async Task<(string Csrf, AccessUser User)> SignInMemberAsync(WebApiTestHost<ApiAssemblyMarker> factory, HttpClient browser, Guid organization)
    {
        var user = await AccessDatabaseFixture.CreateOperatorAsync(factory);
        await database.ExecuteAsync("INSERT INTO access.organizations(id,name,slug) VALUES ($1,'SSO fixture',$2)", organization, "sso-" + organization.ToString("N"));
        await database.ExecuteAsync("INSERT INTO access.memberships(organization_id,user_id,role) VALUES ($1,$2,'owner')", organization, user.Id);
        return (await AccessDatabaseFixture.LoginAsync(browser, user), user);
    }

    private static string AuthorizeUrl(string redirect = Redirect) => QueryHelpers.AddQueryString("/api/sso/authorize", new Dictionary<string, string?>
        { ["clientId"] = "catalogue", ["redirectUri"] = redirect, ["codeChallenge"] = DeskSsoService.Hash(Verifier), ["state"] = State });

    private static async Task<string> CodeAsync(HttpClient browser)
    {
        using var response = await browser.GetAsync(AuthorizeUrl());
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!;
        Assert.Equal(Redirect, location.GetLeftPart(UriPartial.Path));
        var query = QueryHelpers.ParseQuery(location.Query);
        Assert.Equal(State, query["state"].ToString());
        return query["code"].ToString();
    }

    private static Task<HttpResponseMessage> ExchangeAsync(HttpClient server, string code, string? verifier = null) =>
        server.PostAsJsonAsync("/api/sso/exchange", new { clientId = "catalogue", clientSecret = Secret, redirectUri = Redirect, code, codeVerifier = verifier ?? Verifier });
    private static Task<HttpResponseMessage> RefreshAsync(HttpClient server, string token) =>
        server.PostAsJsonAsync("/api/sso/refresh", new { clientId = "catalogue", clientSecret = Secret, refreshToken = token });
    private static async Task<JsonElement> TokenAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
    }
    private static async Task<bool> ActiveAsync(HttpClient server, string ticket) =>
        (await IntrospectAsync(server, ticket)).GetProperty("active").GetBoolean();
    private static async Task<JsonElement> IntrospectAsync(HttpClient server, string ticket)
    {
        using var response = await server.PostAsJsonAsync("/api/integrations/introspect", new { token = ticket, audience = "ocharo-catalogue" });
        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.CacheControl!.NoStore);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
    }
}
