using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace OcharoBrand.Tests;

/// <summary>
/// Brand signs in through Ocharo's Identity BFF: the public kit, runtime config, status, health, static files and the SPA
/// answer anonymously; every other API route needs a session of the bound organization, unsafe methods <c>brand:edit</c>
/// and publication <c>brand:approve</c>.
/// </summary>
public sealed class AccessTests
{
    // The test store seeds no logos, so it has no release yet: the kit routes answer the feed's own 404, never 401 or 403.
    [Theory]
    [InlineData("/api/public/brand", HttpStatusCode.NotFound)]
    [InlineData("/api/public/brand/tokens.css", HttpStatusCode.NotFound)]
    [InlineData("/api/runtime-config", HttpStatusCode.OK)]
    [InlineData("/api/system/status", HttpStatusCode.OK)]
    [InlineData("/api/system/version", HttpStatusCode.OK)]
    [InlineData("/health", HttpStatusCode.OK)]
    public async Task PublicRoutes_ShouldAnswerWithoutASession(string path, HttpStatusCode expected)
    {
        await using var host = BrandHost.Create(new Dictionary<string, string?>());
        using var client = BrandTestIdentity.Anonymous(host.Factory);

        using var response = await client.GetAsync(path);

        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
    }

    [Fact]
    public async Task PrivateApi_ShouldNeedASession_WhileTheSpaAndTheSessionRouteStayAnonymous()
    {
        await using var host = BrandHost.Create(new Dictionary<string, string?>());
        using var client = BrandTestIdentity.Anonymous(host.Factory);

        foreach (var path in new[] { "/api/brand/draft", "/api/brand/consumers", "/api/brand/missing-route" })
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/brand/publish", new { })).StatusCode);
        var session = await BrandTestIdentity.SessionAsync(client);
        Assert.False(session.GetProperty("isAuthenticated").GetBoolean());
        using var page = new HttpRequestMessage(HttpMethod.Get, "/logos");
        page.Headers.Accept.ParseAdd("text/html");
        var spa = await client.SendAsync(page);
        Assert.NotEqual(HttpStatusCode.Unauthorized, spa.StatusCode);
        Assert.NotEqual(HttpStatusCode.Redirect, spa.StatusCode);
    }

    [Fact]
    public async Task Session_ShouldNameThePersonOrganizationPermissionsAndCsrfToken()
    {
        await using var host = BrandHost.Create(new Dictionary<string, string?>());
        using var client = await BrandTestIdentity.SignedIn(host.Factory, BrandTestIdentity.Owner, csrf: false);

        var session = await BrandTestIdentity.SessionAsync(client);

        Assert.True(session.GetProperty("isAuthenticated").GetBoolean());
        Assert.Equal("owner-sub", session.GetProperty("person").GetProperty("sub").GetString());
        Assert.Equal(BrandTestIdentity.Organization, session.GetProperty("organization").GetProperty("id").GetGuid());
        Assert.Equal("owner", session.GetProperty("organization").GetProperty("role").GetString());
        Assert.Equal(new string?[] { "brand:edit", "brand:approve" }, session.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()));
        Assert.False(string.IsNullOrEmpty(session.GetProperty("csrfToken").GetString()));
    }

    [Fact]
    public async Task AnotherOrganization_ShouldBeRefusedEverywhereButTheSessionRoute()
    {
        await using var host = BrandHost.Create(new Dictionary<string, string?>());
        foreach (var person in new[] { BrandTestIdentity.Outsider, BrandTestIdentity.Unaffiliated })
        {
            using var client = await BrandTestIdentity.SignedIn(host.Factory, person);

            var read = await client.GetAsync("/api/brand/draft");
            var write = await client.PutAsJsonAsync("/api/brand/identity", new { });

            Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);
            Assert.Equal("organization_access_required", await CodeAsync(read));
            Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
            Assert.True((await BrandTestIdentity.SessionAsync(client)).GetProperty("isAuthenticated").GetBoolean());
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/runtime-config")).StatusCode);
        }
    }

    [Fact]
    public async Task Members_ShouldReadWithMembershipEditWithBrandEditAndPublishWithBrandApprove()
    {
        await using var host = BrandHost.Create(new Dictionary<string, string?>());
        using var viewer = await BrandTestIdentity.SignedIn(host.Factory, BrandTestIdentity.Viewer);
        using var editor = await BrandTestIdentity.SignedIn(host.Factory, BrandTestIdentity.Editor);
        using var owner = await BrandTestIdentity.SignedIn(host.Factory, BrandTestIdentity.Owner);

        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync("/api/brand/draft")).StatusCode);
        var viewerWrite = await viewer.PostAsJsonAsync("/api/brand/appearances", new { });
        Assert.Equal(HttpStatusCode.Forbidden, viewerWrite.StatusCode);
        Assert.Equal("edit_permission_required", await CodeAsync(viewerWrite));
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsJsonAsync("/api/brand/publish", new { })).StatusCode);

        // An editor writes, so the request reaches the command, which refuses the empty appearance on its merits.
        Assert.Equal(HttpStatusCode.BadRequest, (await editor.PostAsJsonAsync("/api/brand/appearances", new { name = "" })).StatusCode);
        foreach (var path in new[] { "/api/brand/publish", "/api/brand/publish/", "/api/brand/PUBLISH/", "/api/brand/drafts/1/withdraw" })
        {
            var denied = await editor.PostAsJsonAsync(path, new { });
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            Assert.Equal("approval_permission_required", await CodeAsync(denied));
        }

        var draft = await owner.GetFromJsonAsync<JsonElement>("/api/brand/draft");
        var published = await owner.PostAsJsonAsync("/api/brand/publish", new { expectedRevision = draft.GetProperty("revision").GetInt32(), note = "x" });
        Assert.NotEqual(HttpStatusCode.Unauthorized, published.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, published.StatusCode);
    }

    [Fact]
    public async Task UnsafeRequests_ShouldNeedTheSessionsCsrfToken()
    {
        await using var host = BrandHost.Create(new Dictionary<string, string?>());
        using var forger = await BrandTestIdentity.SignedIn(host.Factory, BrandTestIdentity.Owner, csrf: false);
        using var owner = await BrandTestIdentity.SignedIn(host.Factory, BrandTestIdentity.Owner);

        var forged = await forger.PostAsJsonAsync("/api/brand/appearances", new { name = "" });
        forger.DefaultRequestHeaders.Add("X-Csrf-Token", "not-a-token");
        var wrong = await forger.PostAsJsonAsync("/api/brand/appearances", new { name = "" });

        Assert.Equal(HttpStatusCode.BadRequest, forged.StatusCode);
        Assert.Equal("AntiforgeryValidationFailed", await CodeAsync(forged));
        Assert.Equal("AntiforgeryValidationFailed", await CodeAsync(wrong));
        Assert.Equal(HttpStatusCode.OK, (await forger.GetAsync("/api/brand/draft")).StatusCode);
        var accepted = await owner.PostAsJsonAsync("/api/brand/appearances", new { name = "" });
        Assert.NotEqual("AntiforgeryValidationFailed", await CodeAsync(accepted));
    }

    [Fact]
    public async Task ApiResponses_ShouldStayUncachedAndThePublicKitOpenToEveryOrigin()
    {
        await using var host = BrandHost.Create(new Dictionary<string, string?>());
        using var owner = await BrandTestIdentity.SignedIn(host.Factory, BrandTestIdentity.Owner);

        var draft = await owner.GetAsync("/api/brand/draft");
        var kit = await owner.GetAsync("/api/public/brand");

        Assert.True(draft.Headers.CacheControl!.NoStore);
        Assert.Equal("nosniff", draft.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("*", kit.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.False(draft.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Theory]
    [InlineData("DataProtection:KeysPath")]
    [InlineData("Brand:OrganizationId")]
    public async Task DeployedHost_ShouldRefuseToBoot_WithoutItsKeyRingOrOrganization(string key)
    {
        await using var host = BrandHost.Create(new Dictionary<string, string?> { ["DEPLOY_ENVIRONMENT"] = "prod", [key] = null });

        var failure = Record.Exception(() => BrandTestIdentity.Anonymous(host.Factory));

        Assert.True(failure is InvalidOperationException or OptionsValidationException, failure?.ToString());
    }

    private static async Task<string?> CodeAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(text)) return null;
        using var document = JsonDocument.Parse(text);
        return document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("code", out var code)
            ? code.GetString() : null;
    }
}
