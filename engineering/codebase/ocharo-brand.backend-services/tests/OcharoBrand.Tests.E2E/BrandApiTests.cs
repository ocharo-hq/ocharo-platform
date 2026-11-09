using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace OcharoBrand.Tests.E2E;

/// <summary>Full HTTP pipeline over a fresh product database, seeded from generated stand-ins for the approved logo files,
/// as the organization's owner (<see cref="BrandTestIdentity.Owner"/>) with the session's CSRF token. The host's clock
/// stands still (<see cref="FakeTimeProvider"/>), so appearance boundaries and cache lifetimes are exact.</summary>
public sealed class BrandApiTests : IAsyncDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "ocharo-brand-tests", Guid.NewGuid().ToString("N"));
    private readonly FakeTimeProvider time = new(DateTimeOffset.UtcNow);
    private readonly WebApplicationFactory<Program> factory;

    public BrandApiTests()
    {
        var logos = Path.Combine(root, "logos");
        Directory.CreateDirectory(logos);
        File.WriteAllBytes(Path.Combine(logos, "och-logo-approved-ivory-v1.png"), TestArtwork.Png(10));
        File.WriteAllBytes(Path.Combine(logos, "och-logo-black-transparent-v1.png"), TestArtwork.Png(60));
        File.WriteAllBytes(Path.Combine(logos, "och-logo-white-transparent-v1.png"), TestArtwork.Png(110));
        factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>(BrandTestIdentity.Settings(root))
            {
                ["Brand:DataRoot"] = Path.Combine(root, "data"),
                ["Brand:SeedLogoRoot"] = logos,
            }));
            builder.ConfigureServices(BrandTestIdentity.Use);
            builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(time));
        });
    }

    public async ValueTask DisposeAsync()
    {
        await factory.DisposeAsync();
        try { Directory.Delete(root, true); } catch (IOException) { }
    }

    /// <summary>The private API's payload, under the SDK envelope's <c>data</c>.</summary>
    private static JsonElement Data(JsonElement envelope) => envelope.GetProperty("data");

    [Fact]
    public async Task PublicKit_ShouldServeReleaseOneWithApprovedLogosAndRecordedIdentity_WhenStoreIsSeeded()
    {
        using var client = await BrandTestIdentity.SignedIn(factory, BrandTestIdentity.Owner);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/public/brand?locale=ru");
        request.Headers.Add("X-Ocharo-App", "catalogue");

        var kit = await (await client.SendAsync(request)).Content.ReadFromJsonAsync<JsonElement>();
        var favicon = await client.GetAsync("/api/public/brand/logo/favicon");
        var social = await client.GetAsync("/api/public/brand/logo/social");
        var consumers = Data(await client.GetFromJsonAsync<JsonElement>("/api/brand/consumers"));

        kit.GetProperty("name").GetString().Should().Be("Ocharo");
        kit.GetProperty("release").GetInt32().Should().Be(1);
        kit.GetProperty("descriptor").GetString().Should().Be("Современная узбекская мода");
        kit.GetProperty("links")[0].GetProperty("handle").GetString().Should().Be("ocharostile");
        kit.GetProperty("logos").GetProperty("on-light").GetProperty("url").GetString().Should().Be("/api/public/brand/logo/on-light");
        favicon.StatusCode.Should().Be(HttpStatusCode.OK);
        social.StatusCode.Should().Be(HttpStatusCode.OK);
        social.Headers.GetValues("Cross-Origin-Resource-Policy").Single().Should().Be("cross-origin");
        TestArtwork.Sha(await social.Content.ReadAsByteArrayAsync()).Should().Be(TestArtwork.Sha(TestArtwork.Png(10)));
        consumers[0].GetProperty("app").GetString().Should().Be("catalogue");
        consumers[0].GetProperty("current").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Logo_ShouldReachStableUrlOnlyAfterPublish_WhenNewVersionIsUploaded()
    {
        using var client = await BrandTestIdentity.SignedIn(factory, BrandTestIdentity.Owner);
        var draft = Data(await client.GetFromJsonAsync<JsonElement>("/api/brand/draft"));
        var revision = draft.GetProperty("revision").GetInt32();
        var replacement = TestArtwork.Png(200);
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(replacement);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "file", "och-logo-black-v2.png");
        form.Add(new StringContent("Heavier needle for small sizes"), "note");
        var uploaded = Data(await (await client.PostAsync("/api/brand/logos/on-light", form)).Content.ReadFromJsonAsync<JsonElement>());

        var draftPublic = await client.GetAsync($"/api/public/brand/media/{TestArtwork.Sha(replacement)}");
        var draftAdmin = await client.GetAsync($"/api/brand/media/{TestArtwork.Sha(replacement)}");
        var set = await client.PostAsJsonAsync("/api/brand/logos/on-light/current", new { expectedRevision = revision, versionId = uploaded.GetProperty("id").GetString() });
        var beforePublish = TestArtwork.Sha(await client.GetByteArrayAsync("/api/public/brand/logo/on-light"));
        var pending = Data(await client.GetFromJsonAsync<JsonElement>("/api/brand/draft"));
        var stale = await client.PostAsJsonAsync("/api/brand/publish", new { expectedRevision = revision, note = "x" });
        var published = await client.PostAsJsonAsync("/api/brand/publish", new { expectedRevision = revision + 1, note = "New light logo" });
        var afterPublish = TestArtwork.Sha(await client.GetByteArrayAsync("/api/public/brand/logo/on-light"));
        var releasedPublic = await client.GetAsync($"/api/public/brand/media/{TestArtwork.Sha(replacement)}");
        var pinned = await client.GetFromJsonAsync<JsonElement>("/api/public/brand/releases/1");
        var nothingNew = await client.PostAsJsonAsync("/api/brand/publish", new { expectedRevision = revision + 1, note = "again" });
        var releases = Data(await client.GetFromJsonAsync<JsonElement>("/api/brand/releases"));

        draftPublic.StatusCode.Should().Be(HttpStatusCode.NotFound);
        draftAdmin.StatusCode.Should().Be(HttpStatusCode.OK);
        releasedPublic.StatusCode.Should().Be(HttpStatusCode.OK);
        set.StatusCode.Should().Be(HttpStatusCode.OK);
        beforePublish.Should().Be(TestArtwork.Sha(TestArtwork.Png(60)));
        pending.GetProperty("unpublishedChanges").EnumerateArray().Should().Contain(c => c.GetString()!.Contains("on-light"));
        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
        published.StatusCode.Should().Be(HttpStatusCode.OK);
        afterPublish.Should().Be(TestArtwork.Sha(replacement));
        pinned.GetProperty("logos").GetProperty("on-light").GetProperty("sha256").GetString().Should().Be(TestArtwork.Sha(TestArtwork.Png(60)));
        nothingNew.StatusCode.Should().Be(HttpStatusCode.Conflict);
        releases[0].GetProperty("version").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task Appearance_ShouldOverrideLogosAndAccentUntilCancelled_WhenActive()
    {
        using var client = await BrandTestIdentity.SignedIn(factory, BrandTestIdentity.Owner);
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(TestArtwork.Png(240));
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "file", "navruz-on-light.png");
        var festive = Data(await (await client.PostAsync("/api/brand/logos/on-light", form)).Content.ReadFromJsonAsync<JsonElement>());
        var now = time.GetUtcNow();

        var saved = await client.PostAsJsonAsync("/api/brand/appearances", new
        {
            name = "Navro‘z", startsAt = now.AddHours(-1), endsAt = now.AddDays(2),
            overrides = new Dictionary<string, string> { ["on-light"] = festive.GetProperty("id").GetString()! }, accentHex = "#1f7a4d",
        });
        var appearance = Data(await saved.Content.ReadFromJsonAsync<JsonElement>());
        var overlapping = await client.PostAsJsonAsync("/api/brand/appearances", new
        {
            name = "Other", startsAt = now, endsAt = now.AddDays(1), accentHex = "#000000",
        });
        var during = TestArtwork.Sha(await client.GetByteArrayAsync("/api/public/brand/logo/on-light"));
        var startedArtwork = await client.GetAsync($"/api/public/brand/media/{TestArtwork.Sha(TestArtwork.Png(240))}");
        var kit = await client.GetFromJsonAsync<JsonElement>("/api/public/brand");
        var tokens = await client.GetStringAsync("/api/public/brand/tokens.css");
        await client.PostAsJsonAsync($"/api/brand/appearances/{appearance.GetProperty("id").GetString()}/cancel", new { });
        var after = TestArtwork.Sha(await client.GetByteArrayAsync("/api/public/brand/logo/on-light"));

        saved.StatusCode.Should().Be(HttpStatusCode.OK);
        overlapping.StatusCode.Should().Be(HttpStatusCode.Conflict);
        during.Should().Be(TestArtwork.Sha(TestArtwork.Png(240)));
        startedArtwork.StatusCode.Should().Be(HttpStatusCode.OK);
        kit.GetProperty("logos").GetProperty("on-light").GetProperty("fromAppearance").GetBoolean().Should().BeTrue();
        kit.GetProperty("appearance").GetProperty("name").GetString().Should().Be("Navro‘z");
        tokens.Should().Contain("--ocharo-accent: #1f7a4d;");
        after.Should().Be(TestArtwork.Sha(TestArtwork.Png(60)));
    }

    [Fact]
    public async Task Appearance_ShouldKeepArtworkPrivateAndCapCacheLifetimes_WhenItStartsInTheFuture()
    {
        using var client = await BrandTestIdentity.SignedIn(factory, BrandTestIdentity.Owner);
        using var form = new MultipartFormDataContent();
        var artwork = TestArtwork.Png(170);
        var file = new ByteArrayContent(artwork);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "file", "collection-launch.png");
        var upload = Data(await (await client.PostAsync("/api/brand/logos/on-light", form)).Content.ReadFromJsonAsync<JsonElement>());
        var startsAt = time.GetUtcNow().AddSeconds(40);

        var saved = await client.PostAsJsonAsync("/api/brand/appearances", new
        {
            name = "Collection launch", startsAt, endsAt = startsAt.AddDays(1),
            overrides = new Dictionary<string, string> { ["on-light"] = upload.GetProperty("id").GetString()! },
        });
        var embargoed = await client.GetAsync($"/api/public/brand/media/{TestArtwork.Sha(artwork)}");
        var plainLink = await client.PostAsJsonAsync("/api/brand/appearances", new
        {
            name = "Bad link", startsAt = startsAt.AddDays(2), endsAt = startsAt.AddDays(3), linkLabel = "Open", linkUrl = "http://example.com/",
        });
        var badgeOnly = await client.PostAsJsonAsync("/api/brand/appearances", new
        {
            name = "Open studio", startsAt = time.GetUtcNow().AddMinutes(-1), endsAt = startsAt.AddSeconds(-1),
            linkLabel = "Open studio", linkUrl = "https://t.me/Ocharo_Style",
        });
        var kit = await client.GetAsync("/api/public/brand");
        var logo = await client.GetAsync("/api/public/brand/logo/on-light");

        saved.StatusCode.Should().Be(HttpStatusCode.OK);
        embargoed.StatusCode.Should().Be(HttpStatusCode.NotFound);
        plainLink.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        badgeOnly.StatusCode.Should().Be(HttpStatusCode.OK);
        var active = (await kit.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("appearance");
        active.GetProperty("linkLabel").GetString().Should().Be("Open studio");
        active.GetProperty("linkUrl").GetString().Should().Be("https://t.me/Ocharo_Style");
        (await kit.Content.ReadAsStringAsync()).Should().NotContain(TestArtwork.Sha(artwork));
        // The next boundary is the badge's end, 39 seconds out.
        logo.Headers.CacheControl!.MaxAge.Should().Be(TimeSpan.FromSeconds(39));
        kit.Headers.CacheControl!.MaxAge.Should().Be(TimeSpan.FromSeconds(39));
    }

    [Fact]
    public async Task IdentitySave_ShouldValidateCheckRevisionAndRequireCsrfToken_WhenEdited()
    {
        using var client = await BrandTestIdentity.SignedIn(factory, BrandTestIdentity.Owner);
        var draft = Data(await client.GetFromJsonAsync<JsonElement>("/api/brand/draft"));
        var revision = draft.GetProperty("revision").GetInt32();
        var identity = JsonSerializer.Deserialize<Dictionary<string, object?>>(draft.GetProperty("draft").GetProperty("identity").GetRawText())!;
        identity["palette"] = new[] { new { key = "accent", name = "Accent", hex = "red", role = "accent" } };

        var invalid = await client.PutAsJsonAsync("/api/brand/identity", new { expectedRevision = revision, identity });
        identity["palette"] = new[] { new { key = "accent", name = "Accent", hex = "#aa3322", role = "accent" } };
        var saved = await client.PutAsJsonAsync("/api/brand/identity", new { expectedRevision = revision, identity });
        var stale = await client.PutAsJsonAsync("/api/brand/identity", new { expectedRevision = revision, identity });
        using var forger = await BrandTestIdentity.SignedIn(factory, BrandTestIdentity.Owner, csrf: false);
        using var crossOrigin = new HttpRequestMessage(HttpMethod.Put, "/api/brand/identity") { Content = JsonContent.Create(new { expectedRevision = revision + 1, identity }) };
        crossOrigin.Headers.Add("Origin", "https://evil.example");
        var rejected = await forger.SendAsync(crossOrigin);
        var publicKit = await client.GetAsync("/api/public/brand");

        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        saved.StatusCode.Should().Be(HttpStatusCode.OK);
        stale.StatusCode.Should().Be(HttpStatusCode.Conflict);
        rejected.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await rejected.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("AntiforgeryValidationFailed");
        publicKit.Headers.GetValues("Access-Control-Allow-Origin").Single().Should().Be("*");
    }

    [Fact]
    public async Task Upload_ShouldAnswer415And413_WhenFileIsNotAnImageOrIsEmpty()
    {
        using var client = await BrandTestIdentity.SignedIn(factory, BrandTestIdentity.Owner);

        using var text = await UploadAsync(client, "not an image"u8.ToArray(), "notes.png");
        using var empty = await UploadAsync(client, [], "empty.png");

        text.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);
        (await text.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("brandCode").GetString().Should().Be("image_type_unsupported");
        empty.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        (await empty.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("brandCode").GetString().Should().Be("image_too_large");
    }

    [Fact]
    public async Task ApiResponses_ShouldWrapPrivatePayloadInDataAndLeavePublicKitBare_WhenRead()
    {
        using var client = await BrandTestIdentity.SignedIn(factory, BrandTestIdentity.Owner);

        var draft = await client.GetFromJsonAsync<JsonElement>("/api/brand/draft");
        var kit = await client.GetFromJsonAsync<JsonElement>("/api/public/brand");
        var pinned = await client.GetFromJsonAsync<JsonElement>("/api/public/brand/releases/1");

        draft.GetProperty("data").GetProperty("latestRelease").GetInt32().Should().Be(1);
        kit.TryGetProperty("data", out _).Should().BeFalse();
        kit.GetProperty("name").GetString().Should().Be("Ocharo");
        pinned.GetProperty("release").GetInt32().Should().Be(1);
        // Nulls are omitted on the wire: the seed has no tagline and no appearance runs.
        kit.TryGetProperty("tagline", out _).Should().BeFalse();
        kit.TryGetProperty("appearance", out _).Should().BeFalse();
    }

    private static async Task<HttpResponseMessage> UploadAsync(HttpClient client, byte[] bytes, string name)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "file", name);
        return await client.PostAsync("/api/brand/logos/on-light", form);
    }

    [Theory]
    [InlineData("GET", "/api/brand/missing-contract-route", null, null, 404)]
    [InlineData("PATCH", "/api/brand/draft", null, null, 404)]
    [InlineData("PUT", "/api/brand/identity", "application/json", "{", 400)]
    [InlineData("PUT", "/api/brand/identity", "text/plain", "invalid", 415)]
    public async Task Errors_ShouldRemainProblemJson_WhenHtmlIsRequested(string method, string path, string? contentType, string? body, int expected)
    {
        using var client = await BrandTestIdentity.SignedIn(factory, BrandTestIdentity.Owner);
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        request.Headers.Accept.ParseAdd("text/html");
        if (body is not null) request.Content = new StringContent(body, System.Text.Encoding.UTF8, contentType!);
        using var response = await client.SendAsync(request);
        ((int)response.StatusCode).Should().Be(expected);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
        response.Headers.CacheControl?.NoStore.Should().BeTrue();
        var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        problem.GetProperty("status").GetInt32().Should().Be(expected);
        string.IsNullOrWhiteSpace(problem.GetProperty("type").GetString()).Should().BeFalse();
        string.IsNullOrWhiteSpace(problem.GetProperty("title").GetString()).Should().BeFalse();
        problem.GetProperty("instance").GetString().Should().Be(path);
        string.IsNullOrWhiteSpace(problem.GetProperty("traceId").GetString()).Should().BeFalse();
        string.IsNullOrWhiteSpace(problem.GetProperty("requestId").GetString()).Should().BeFalse();
    }
}
