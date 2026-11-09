using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace OcharoBrand.Tests.E2E;

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
    [InlineData("/health", HttpStatusCode.OK)]
    public async Task PublicRoutes_ShouldAnswerExpectedStatus_WhenNoSessionExists(string path, HttpStatusCode expected)
    {
        await using var host = BrandHost.Create(new Dictionary<string, string?>());
        using var client = BrandTestIdentity.Anonymous(host.Factory);

        using var response = await client.GetAsync(path);

        response.StatusCode.Should().Be(expected);
        response.Headers.GetValues("X-Content-Type-Options").Single().Should().Be("nosniff");
    }

    [Fact]
    public async Task PrivateApi_ShouldAnswer401WhileSpaAndSessionRouteStayAnonymous_WhenNoSessionExists()
    {
        await using var host = BrandHost.Create(new Dictionary<string, string?>());
        using var client = BrandTestIdentity.Anonymous(host.Factory);

        foreach (var path in new[] { "/api/brand/draft", "/api/brand/consumers", "/api/brand/missing-route" })
            (await client.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsJsonAsync("/api/brand/publish", new { })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var session = await BrandTestIdentity.SessionAsync(client);
        session.GetProperty("isAuthenticated").GetBoolean().Should().BeFalse();
        using var page = new HttpRequestMessage(HttpMethod.Get, "/logos");
        page.Headers.Accept.ParseAdd("text/html");
        var spa = await client.SendAsync(page);
        spa.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        spa.StatusCode.Should().NotBe(HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task SignalRoute_ShouldAnswer401_WhenPostIsUnsignedWithoutSession()
    {
        await using var host = BrandHost.Create(new Dictionary<string, string?> { ["Identity:Client:SignalUrl"] = "https://app.test/api/identity/signal" });
        using var client = BrandTestIdentity.Anonymous(host.Factory);

        var response = await client.PostAsJsonAsync("/api/identity/signal", new { });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Session_ShouldNamePersonOrganizationPermissionsAndCsrfToken_WhenSignedIn()
    {
        await using var host = BrandHost.Create(new Dictionary<string, string?>());
        using var client = await BrandTestIdentity.SignedIn(host.Factory, BrandTestIdentity.Owner, csrf: false);

        var session = await BrandTestIdentity.SessionAsync(client);

        session.GetProperty("isAuthenticated").GetBoolean().Should().BeTrue();
        session.GetProperty("person").GetProperty("sub").GetString().Should().Be("owner-sub");
        session.GetProperty("organization").GetProperty("id").GetGuid().Should().Be(BrandTestIdentity.Organization);
        session.GetProperty("organization").GetProperty("role").GetString().Should().Be("owner");
        session.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()).Should().BeEquivalentTo(new string?[] { "brand:edit", "brand:approve" }, options => options.WithStrictOrdering());
        string.IsNullOrEmpty(session.GetProperty("csrfToken").GetString()).Should().BeFalse();
    }

    [Fact]
    public async Task PrivateApi_ShouldAnswer403ExceptSessionRoute_WhenPersonBelongsToAnotherOrganization()
    {
        await using var host = BrandHost.Create(new Dictionary<string, string?>());
        foreach (var person in new[] { BrandTestIdentity.Outsider, BrandTestIdentity.Unaffiliated })
        {
            using var client = await BrandTestIdentity.SignedIn(host.Factory, person);

            var read = await client.GetAsync("/api/brand/draft");
            var write = await client.PutAsJsonAsync("/api/brand/identity", new { });

            read.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await BrandCodeAsync(read)).Should().Be("organization_access_required");
            (await CodeAsync(read)).Should().Be("Forbidden");
            write.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await BrandTestIdentity.SessionAsync(client)).GetProperty("isAuthenticated").GetBoolean().Should().BeTrue();
            (await client.GetAsync("/api/runtime-config")).StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task Members_ShouldReadEditAndPublishByPermission_WhenSignedIn()
    {
        await using var host = BrandHost.Create(new Dictionary<string, string?>());
        using var viewer = await BrandTestIdentity.SignedIn(host.Factory, BrandTestIdentity.Viewer);
        using var editor = await BrandTestIdentity.SignedIn(host.Factory, BrandTestIdentity.Editor);
        using var owner = await BrandTestIdentity.SignedIn(host.Factory, BrandTestIdentity.Owner);

        (await viewer.GetAsync("/api/brand/draft")).StatusCode.Should().Be(HttpStatusCode.OK);
        var viewerWrite = await viewer.PostAsJsonAsync("/api/brand/appearances", new { });
        viewerWrite.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await BrandCodeAsync(viewerWrite)).Should().Be("edit_permission_required");
        (await viewer.PostAsJsonAsync("/api/brand/publish", new { })).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // An editor writes, so the request reaches the command, which refuses the empty appearance on its merits.
        (await editor.PostAsJsonAsync("/api/brand/appearances", new { name = "" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        foreach (var path in new[] { "/api/brand/publish", "/api/brand/publish/", "/api/brand/PUBLISH/", "/api/brand/drafts/1/withdraw" })
        {
            var denied = await editor.PostAsJsonAsync(path, new { });
            denied.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await BrandCodeAsync(denied)).Should().Be("approval_permission_required");
        }

        var draft = (await owner.GetFromJsonAsync<JsonElement>("/api/brand/draft")).GetProperty("data");
        var published = await owner.PostAsJsonAsync("/api/brand/publish", new { expectedRevision = draft.GetProperty("revision").GetInt32(), note = "x" });
        published.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        published.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UnsafeRequests_ShouldAnswer400_WhenCsrfTokenIsMissingOrWrong()
    {
        await using var host = BrandHost.Create(new Dictionary<string, string?>());
        using var forger = await BrandTestIdentity.SignedIn(host.Factory, BrandTestIdentity.Owner, csrf: false);
        using var owner = await BrandTestIdentity.SignedIn(host.Factory, BrandTestIdentity.Owner);

        var forged = await forger.PostAsJsonAsync("/api/brand/appearances", new { name = "" });
        forger.DefaultRequestHeaders.Add("X-Csrf-Token", "not-a-token");
        var wrong = await forger.PostAsJsonAsync("/api/brand/appearances", new { name = "" });

        forged.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await CodeAsync(forged)).Should().Be("AntiforgeryValidationFailed");
        (await CodeAsync(wrong)).Should().Be("AntiforgeryValidationFailed");
        (await forger.GetAsync("/api/brand/draft")).StatusCode.Should().Be(HttpStatusCode.OK);
        var accepted = await owner.PostAsJsonAsync("/api/brand/appearances", new { name = "" });
        (await CodeAsync(accepted)).Should().NotBe("AntiforgeryValidationFailed");
    }

    [Fact]
    public async Task Health_ShouldReportDatabaseReady_WhenNoSessionExists()
    {
        await using var host = BrandHost.Create(new Dictionary<string, string?>());
        using var client = BrandTestIdentity.Anonymous(host.Factory);

        using var health = await client.GetAsync("/health");

        health.StatusCode.Should().Be(HttpStatusCode.OK);
        (await health.Content.ReadAsStringAsync()).Should().Be("Healthy");
    }

    [Fact]
    public async Task ApiResponses_ShouldStayUncachedAndKitOpenToEveryOrigin_WhenRead()
    {
        await using var host = BrandHost.Create(new Dictionary<string, string?>());
        using var owner = await BrandTestIdentity.SignedIn(host.Factory, BrandTestIdentity.Owner);

        var draft = await owner.GetAsync("/api/brand/draft");
        var kit = await owner.GetAsync("/api/public/brand");

        draft.Headers.CacheControl!.NoStore.Should().BeTrue();
        draft.Headers.GetValues("X-Content-Type-Options").Single().Should().Be("nosniff");
        kit.Headers.GetValues("Access-Control-Allow-Origin").Single().Should().Be("*");
        draft.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
        // Other origins embed the kit's files without CORS; the private API stays same-origin.
        kit.Headers.GetValues("Cross-Origin-Resource-Policy").Single().Should().Be("cross-origin");
        draft.Headers.GetValues("Cross-Origin-Resource-Policy").Single().Should().Be("same-origin");
    }

    [Theory]
    [InlineData("ConnectionStrings:Database")]
    [InlineData("Brand:OrganizationId")]
    public async Task DeployedHost_ShouldRefuseToBoot_WhenDatabaseOrOrganizationIsMissing(string key)
    {
        await using var host = BrandHost.Create(new Dictionary<string, string?> { ["DEPLOY_ENVIRONMENT"] = "prod", [key] = null });

        var failure = Record.Exception(() => BrandTestIdentity.Anonymous(host.Factory));

        (failure is InvalidOperationException or OptionsValidationException).Should().BeTrue("{0}", failure?.ToString());
    }

    /// <summary>The problem's SDK <c>code</c>, named for its error kind.</summary>
    private static Task<string?> CodeAsync(HttpResponseMessage response) => ExtensionAsync(response, "code");

    /// <summary>The problem's <c>brandCode</c>, naming which Brand refusal it is.</summary>
    private static Task<string?> BrandCodeAsync(HttpResponseMessage response) => ExtensionAsync(response, "brandCode");

    private static async Task<string?> ExtensionAsync(HttpResponseMessage response, string name)
    {
        var text = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(text)) return null;
        using var document = JsonDocument.Parse(text);
        return document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty(name, out var value)
            ? value.GetString() : null;
    }
}
