using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Ocharo.Sso.Tests;

public sealed class SsoFlowTests
{
    [Fact]
    public async Task Login_UsesFixedDeskPkceAndAppOnlyCookies_AndRejectsCallbackReplay()
    {
        using var app = await TestApp.Start();
        var cookie = await app.Login();
        Assert.StartsWith("__Host-ocharo-catalogue=", cookie);
        Assert.DoesNotContain(app.Desk.Secret, cookie);
        var response = await app.Send("/api/sso/session", cookie);
        var data = await Data(response);
        Assert.True(data.GetProperty("authenticated").GetBoolean());
        Assert.Equal(TestApp.Org.ToString(), data.GetProperty("organizationId").GetString());
        var json = data.GetRawText();
        Assert.DoesNotContain("refresh", json);
        Assert.DoesNotContain("ticket", json);
        Assert.Equal(1, app.Desk.Exchanges);
        var replay = await app.Send(app.Callback!, app.Correlation);
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
        Assert.Equal(1, app.Desk.Exchanges);
    }
    [Theory]
    [InlineData("//evil.example/")]
    [InlineData("/\\evil.example/")]
    [InlineData("/%2f%2fevil.example/")]
    [InlineData("/api/sso/callback")]
    public async Task Login_RejectsUnsafeReturnDestinations(string returnUrl)
    {
        using var app = await TestApp.Start();
        var response = await app.Client.GetAsync("/api/sso/login?returnUrl=" + Uri.EscapeDataString(returnUrl));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, app.Desk.Exchanges);
    }
    [Fact]
    public async Task Callback_RequiresSameBrowserCorrelationAndExactClientOrigin()
    {
        using var app = await TestApp.Start();
        await app.StartLogin();
        var noCookie = await app.Client.GetAsync(app.Callback);
        Assert.Equal(HttpStatusCode.BadRequest, noCookie.StatusCode);
        Assert.Equal(0, app.Desk.Exchanges);
        var mismatch = new HttpRequestMessage(HttpMethod.Get, "https://other.example/api/sso/login");
        Assert.Equal(HttpStatusCode.BadRequest, (await app.Client.SendAsync(mismatch)).StatusCode);
    }
    [Fact]
    public async Task BrowserWrites_RequireSessionCsrfAndSameOrigin_AndLiveMembership()
    {
        using var app = await TestApp.Start();
        var cookie = await app.Login();
        var data = await Data(await app.Send("/api/sso/session", cookie));
        var csrf = data.GetProperty("csrfToken").GetString();
        Assert.Equal(HttpStatusCode.Forbidden, (await app.Send("/api/private", cookie, HttpMethod.Post)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await app.Send("/api/private", cookie, HttpMethod.Post, csrf, "https://foreign.example")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await app.Send("/api/private", cookie, HttpMethod.Post, csrf, "https://catalogue.example")).StatusCode);
        app.Desk.Active = false;
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.Send("/api/private", cookie)).StatusCode);
        app.Desk.Active = true;
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.Send("/api/private", cookie)).StatusCode);
    }
    [Fact]
    public async Task FixedOrganizationAndReadOnlyMembership_AreEnforced()
    {
        using var app = await TestApp.Start();
        app.Desk.Organization = Guid.NewGuid();
        await app.StartLogin();
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.Send(app.Callback!, app.Correlation)).StatusCode);
        app.Desk.Organization = TestApp.Org;
        app.Desk.CanEdit = false;
        var cookie = await app.Login();
        var data = await Data(await app.Send("/api/sso/session", cookie));
        Assert.Equal(HttpStatusCode.Forbidden, (await app.Send("/api/private", cookie, HttpMethod.Post, data.GetProperty("csrfToken").GetString())).StatusCode);
    }
    [Fact]
    public async Task ConcurrentRequests_RotateRefreshOnce_WithoutReturningTokensToBrowser()
    {
        using var app = await TestApp.Start();
        var cookie = await app.Login();
        app.Clock.Now = app.Clock.Now.AddMinutes(4).AddSeconds(40);
        var responses = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => app.Send("/api/private", cookie)));
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        Assert.Equal(1, app.Desk.Refreshes);
        Assert.True(app.Desk.Introspections >= 13);
    }
    [Fact]
    public async Task PublicReadsStayPublic_PrivateApisDenyAnonymous_AndNavigationRestoresDeskSession()
    {
        using var app = await TestApp.Start();
        Assert.Equal(HttpStatusCode.OK, (await app.Client.GetAsync("/api/public/feed")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.Client.GetAsync("/api/private")).StatusCode);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/catalogues");
        request.Headers.Accept.ParseAdd("text/html");
        var redirect = await app.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Redirect, redirect.StatusCode);
        Assert.Equal("desk.example", redirect.Headers.Location!.Host);
    }
    [Fact]
    public async Task LogoutRequiresCsrf_RevokesGrant_AndClearsLocalSession()
    {
        using var app = await TestApp.Start();
        var cookie = await app.Login();
        var data = await Data(await app.Send("/api/sso/session", cookie));
        Assert.Equal(HttpStatusCode.Forbidden, (await app.Send("/api/sso/logout", cookie, HttpMethod.Post)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await app.Send("/api/sso/logout", cookie, HttpMethod.Post, data.GetProperty("csrfToken").GetString())).StatusCode);
        Assert.Equal(1, app.Desk.Revocations);
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.Send("/api/private", cookie)).StatusCode);
    }
    [Fact]
    public async Task DisabledMode_DoesNotContactDeskOrChangeExistingPilot()
    {
        using var app = await TestApp.Start(enabled:false);
        Assert.Equal(HttpStatusCode.OK, (await app.Client.GetAsync("/api/private")).StatusCode);
        Assert.False((await Data(await app.Client.GetAsync("/api/sso/session"))).GetProperty("enabled").GetBoolean());
        Assert.Equal(HttpStatusCode.NotFound, (await app.Client.GetAsync("/api/sso/login")).StatusCode);
        Assert.Equal(0, app.Desk.Introspections);
    }
    [Fact]
    public async Task AppRestart_DoesNotAcceptOldCookie_AndBearerNeedsLiveAudienceAuthority()
    {
        using var first = await TestApp.Start();
        var cookie = await first.Login();
        using var restarted = await TestApp.Start();
        Assert.Equal(HttpStatusCode.Unauthorized, (await restarted.Send("/api/private", cookie)).StatusCode);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/private");
        request.Headers.Authorization = new("Bearer", "invalid-ticket");
        Assert.Equal(HttpStatusCode.Unauthorized, (await restarted.Client.SendAsync(request)).StatusCode);
    }
    [Fact]
    public async Task OptionalResourceResolution_SuppliesCookieAuthorityWithoutBlockingPublishedAnonymousMedia()
    {
        using var app = await TestApp.Start();
        var anonymous = await app.Client.GetFromJsonAsync<JsonElement>("/media/public.png");
        Assert.False(anonymous.GetProperty("authenticated").GetBoolean());
        var cookie = await app.Login();
        var response = await app.Send("/media/draft.png", cookie);
        Assert.True((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("authenticated").GetBoolean());
    }
    [Fact]
    public async Task OversizedBackchannelResponse_IsRejectedBeforeAnAppSessionIsCreated()
    {
        using var app = await TestApp.Start();
        app.Desk.OversizedResponse = true;
        await app.StartLogin();
        var response = await app.Send(app.Callback!, app.Correlation);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.DoesNotContain(response.Headers.GetValues("Set-Cookie"), value => value.StartsWith("__Host-ocharo-catalogue=", StringComparison.Ordinal));
    }
    private static async Task<JsonElement> Data(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
    }
    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now = DateTimeOffset.Parse("2026-10-09T12:00:00Z");
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class TestApp(IHost host, HttpClient client, FakeDesk desk, TestClock clock) : IDisposable
    {
        public static readonly Guid Org = Guid.Parse("09b4f71f-19d1-42d4-8ce0-f8eaa6b69b14");
        public HttpClient Client => client;
        public FakeDesk Desk => desk;
        public TestClock Clock => clock;
        public string? Correlation { get; private set; }
        public string? Callback { get; private set; }
        public static async Task<TestApp> Start(bool enabled=true)
        {
            var clock = new TestClock();
            var desk = new FakeDesk(clock);
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
            {
                ["Identity:Sso:Enabled"] = enabled.ToString(), ["Identity:Sso:DeskOrigin"] = "https://desk.example",
                ["Identity:Sso:ClientOrigin"] = "https://catalogue.example", ["Identity:Sso:ClientId"] = "catalogue",
                ["Identity:Sso:ClientSecret"] = desk.Secret, ["Identity:Sso:OrganizationId"] = Org.ToString()
            }).Build();
            var host = await new HostBuilder().ConfigureWebHost(web => web.UseTestServer().ConfigureServices(services =>
            {
                services.AddSingleton<IConfiguration>(config);
                services.AddSingleton<TimeProvider>(clock);
                services.AddRouting();
                services.AddOcharoSso(config, "catalogue", "ocharo-catalogue", true);
                services.AddHttpClient<SsoBackchannel>().ConfigurePrimaryHttpMessageHandler(() => desk);
            }).Configure(app =>
            {
                app.UseOcharoSso(http => HttpMethods.IsGet(http.Request.Method) && http.Request.Path.StartsWithSegments("/api/public"),
                    http => http.Request.Path.StartsWithSegments("/media"));
                app.Run(http => http.Response.WriteAsJsonAsync(new { ok = true, authenticated = http.User.Identity?.IsAuthenticated == true }));
            })).StartAsync();
            var client = host.GetTestClient();
            client.BaseAddress = new Uri("https://catalogue.example");
            return new(host, client, desk, clock);
        }
        public async Task StartLogin()
        {
            var response = await client.GetAsync("/api/sso/login?returnUrl=/catalogues");
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var location = response.Headers.Location!;
            Assert.Equal("https://desk.example/api/sso/authorize", location.GetLeftPart(UriPartial.Path));
            var query = QueryHelpers.ParseQuery(location.Query);
            Assert.Equal("https://catalogue.example/api/sso/callback", query["redirectUri"].ToString());
            desk.Challenge = query["codeChallenge"].ToString();
            var cookie = response.Headers.GetValues("Set-Cookie").Single();
            Assert.Contains("secure", cookie); Assert.Contains("httponly", cookie); Assert.Contains("samesite=lax", cookie); Assert.DoesNotContain("domain=", cookie);
            Correlation = cookie.Split(';')[0];
            Callback = "/api/sso/callback?state=" + query["state"] + "&code=" + new string('c',43);
        }
        public async Task<string> Login()
        {
            await StartLogin();
            var response = await Send(Callback!, Correlation);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/catalogues", response.Headers.Location!.OriginalString);
            return response.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith("__Host-ocharo-catalogue=", StringComparison.Ordinal)).Split(';')[0];
        }
        public async Task<HttpResponseMessage> Send(string path, string? cookie, HttpMethod? method=null, string? csrf=null, string? origin=null)
        {
            using var request = new HttpRequestMessage(method ?? HttpMethod.Get,path);
            if (cookie is not null) request.Headers.Add("Cookie",cookie);
            if (csrf is not null) request.Headers.Add("X-CSRF-Token",csrf);
            if (origin is not null) request.Headers.Add("Origin",origin);
            return await client.SendAsync(request);
        }
        public void Dispose() { client.Dispose(); host.Dispose(); }
    }
    private sealed class FakeDesk(TestClock clock) : HttpMessageHandler
    {
        public string Secret = new('s',48);
        public string? Challenge;
        public int Exchanges,Refreshes,Introspections,Revocations;
        public bool Active=true, CanEdit=true, OversizedResponse;
        public Guid Organization = TestApp.Org;
        private string refresh = new('r',43);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("desk.example", request.RequestUri!.Host);
            Assert.False(request.Headers.Contains("Cookie"));
            var json = await request.Content!.ReadFromJsonAsync<JsonElement>(cancellationToken:cancellationToken);
            switch (request.RequestUri.AbsolutePath)
            {
                case "/api/sso/exchange":
                    Assert.Equal(Secret,json.GetProperty("clientSecret").GetString());
                    Assert.Equal(Challenge, WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(json.GetProperty("codeVerifier").GetString()!))));
                    Exchanges++;
                    return Grant();
                case "/api/sso/refresh":
                    Assert.Equal(Secret,json.GetProperty("clientSecret").GetString());
                    Assert.Equal(refresh,json.GetProperty("refreshToken").GetString());
                    Interlocked.Increment(ref Refreshes);
                    await Task.Delay(10,cancellationToken);
                    refresh = new string('t',43);
                    return Grant();
                case "/api/sso/revoke":
                    Assert.Equal(refresh,json.GetProperty("refreshToken").GetString());
                    Revocations++;
                    return new(HttpStatusCode.NoContent);
                case "/api/integrations/introspect":
                    Interlocked.Increment(ref Introspections);
                    Assert.Equal("ocharo-catalogue",json.GetProperty("audience").GetString());
                    return Json(new { data = new { active = Active && json.GetProperty("token").GetString()=="synthetic-ticket", userId="operator", organizationId=Organization, role="owner", canEdit=CanEdit, canApprove=true, expiresAt=clock.Now.AddMinutes(5) } });
                default: throw new InvalidOperationException("Unexpected backchannel");
            }
        }
        private HttpResponseMessage Grant() => Json(new { padding = OversizedResponse ? new string('x', 40_000) : "", data = new { ticket="synthetic-ticket", expiresAt=clock.Now.AddMinutes(5), refreshToken=refresh, sessionExpiresAt=clock.Now.AddHours(1) } });
        private static HttpResponseMessage Json(object value) => new(HttpStatusCode.OK) { Content=JsonContent.Create(value) };
    }
}
