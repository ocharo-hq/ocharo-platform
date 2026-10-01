using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using SkiaSharp;

namespace OcharoBrand.Tests;

/// <summary>Full HTTP pipeline over a temp store, seeded from generated stand-ins for the approved logo files.</summary>
public sealed class BrandApiTests : IAsyncDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "ocharo-brand-tests", Guid.NewGuid().ToString("N"));
    private readonly WebApplicationFactory<Program> factory;

    public BrandApiTests()
    {
        var logos = Path.Combine(root, "logos");
        Directory.CreateDirectory(logos);
        File.WriteAllBytes(Path.Combine(logos, "och-logo-approved-ivory-v1.png"), Png(10));
        File.WriteAllBytes(Path.Combine(logos, "och-logo-black-transparent-v1.png"), Png(60));
        File.WriteAllBytes(Path.Combine(logos, "och-logo-white-transparent-v1.png"), Png(110));
        factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:OcharoBrand"] = $"Data Source={Path.Combine(root, "brand.db")};Pooling=False",
                ["Brand:DataRoot"] = Path.Combine(root, "data"),
                ["Brand:SeedLogoRoot"] = logos,
            }));
        });
    }

    public async ValueTask DisposeAsync()
    {
        await factory.DisposeAsync();
        try { Directory.Delete(root, true); } catch (IOException) { }
    }

    private static byte[] Png(byte shade)
    {
        using var bitmap = new SKBitmap(80, 40, SKColorType.Rgba8888, SKAlphaType.Premul);
        bitmap.Erase(new SKColor(shade, (byte)(255 - shade), 40, 255));
        using var image = SKImage.FromBitmap(bitmap);
        return image.Encode(SKEncodedImageFormat.Png, 100).ToArray();
    }

    private static string Sha(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    [Fact]
    public async Task Seed_publishes_release_one_with_the_approved_logos_and_recorded_identity()
    {
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/public/brand?locale=ru");
        request.Headers.Add("X-Ocharo-App", "catalogue");

        var kit = await (await client.SendAsync(request)).Content.ReadFromJsonAsync<JsonElement>();
        var favicon = await client.GetAsync("/api/public/brand/logo/favicon");
        var social = await client.GetAsync("/api/public/brand/logo/social");
        var consumers = await client.GetFromJsonAsync<JsonElement>("/api/brand/consumers");

        Assert.Equal("Ocharo", kit.GetProperty("name").GetString());
        Assert.Equal(1, kit.GetProperty("release").GetInt32());
        Assert.Equal("Современная узбекская мода", kit.GetProperty("descriptor").GetString());
        Assert.Equal("ocharostile", kit.GetProperty("links")[0].GetProperty("handle").GetString());
        Assert.Equal("/api/public/brand/logo/on-light", kit.GetProperty("logos").GetProperty("on-light").GetProperty("url").GetString());
        Assert.Equal(HttpStatusCode.OK, favicon.StatusCode);
        Assert.Equal(HttpStatusCode.OK, social.StatusCode);
        Assert.Equal(Sha(Png(10)), Sha(await social.Content.ReadAsByteArrayAsync()));
        Assert.Equal("catalogue", consumers[0].GetProperty("app").GetString());
        Assert.True(consumers[0].GetProperty("current").GetBoolean());
    }

    [Fact]
    public async Task A_new_logo_reaches_the_stable_url_only_after_publishing()
    {
        var client = factory.CreateClient();
        var draft = await client.GetFromJsonAsync<JsonElement>("/api/brand/draft");
        var revision = draft.GetProperty("revision").GetInt32();
        var replacement = Png(200);
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(replacement);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "file", "och-logo-black-v2.png");
        form.Add(new StringContent("Heavier needle for small sizes"), "note");
        var uploaded = await (await client.PostAsync("/api/brand/logos/on-light", form)).Content.ReadFromJsonAsync<JsonElement>();

        var draftPublic = await client.GetAsync($"/api/public/brand/media/{Sha(replacement)}");
        var draftAdmin = await client.GetAsync($"/api/brand/media/{Sha(replacement)}");
        var set = await client.PostAsJsonAsync("/api/brand/logos/on-light/current", new { expectedRevision = revision, versionId = uploaded.GetProperty("id").GetString() });
        var beforePublish = Sha(await client.GetByteArrayAsync("/api/public/brand/logo/on-light"));
        var pending = await client.GetFromJsonAsync<JsonElement>("/api/brand/draft");
        var stale = await client.PostAsJsonAsync("/api/brand/publish", new { expectedRevision = revision, note = "x" });
        var published = await client.PostAsJsonAsync("/api/brand/publish", new { expectedRevision = revision + 1, note = "New light logo" });
        var afterPublish = Sha(await client.GetByteArrayAsync("/api/public/brand/logo/on-light"));
        var releasedPublic = await client.GetAsync($"/api/public/brand/media/{Sha(replacement)}");
        var pinned = await client.GetFromJsonAsync<JsonElement>("/api/public/brand/releases/1");
        var nothingNew = await client.PostAsJsonAsync("/api/brand/publish", new { expectedRevision = revision + 1, note = "again" });
        var releases = await client.GetFromJsonAsync<JsonElement>("/api/brand/releases");

        Assert.Equal(HttpStatusCode.NotFound, draftPublic.StatusCode);
        Assert.Equal(HttpStatusCode.OK, draftAdmin.StatusCode);
        Assert.Equal(HttpStatusCode.OK, releasedPublic.StatusCode);
        Assert.Equal(HttpStatusCode.OK, set.StatusCode);
        Assert.Equal(Sha(Png(60)), beforePublish);
        Assert.Contains(pending.GetProperty("unpublishedChanges").EnumerateArray(), c => c.GetString()!.Contains("on-light"));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal(HttpStatusCode.OK, published.StatusCode);
        Assert.Equal(Sha(replacement), afterPublish);
        Assert.Equal(Sha(Png(60)), pinned.GetProperty("logos").GetProperty("on-light").GetProperty("sha256").GetString());
        Assert.Equal(HttpStatusCode.Conflict, nothingNew.StatusCode);
        Assert.Equal(2, releases[0].GetProperty("version").GetInt32());
    }

    [Fact]
    public async Task An_active_appearance_overrides_logos_and_accent_until_cancelled()
    {
        var client = factory.CreateClient();
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Png(240));
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "file", "navruz-on-light.png");
        var festive = await (await client.PostAsync("/api/brand/logos/on-light", form)).Content.ReadFromJsonAsync<JsonElement>();
        var now = DateTimeOffset.UtcNow;

        var saved = await client.PostAsJsonAsync("/api/brand/appearances", new
        {
            name = "Navro‘z", startsAt = now.AddHours(-1), endsAt = now.AddDays(2),
            overrides = new Dictionary<string, string> { ["on-light"] = festive.GetProperty("id").GetString()! }, accentHex = "#1f7a4d",
        });
        var appearance = await saved.Content.ReadFromJsonAsync<JsonElement>();
        var overlapping = await client.PostAsJsonAsync("/api/brand/appearances", new
        {
            name = "Other", startsAt = now, endsAt = now.AddDays(1), accentHex = "#000000",
        });
        var during = Sha(await client.GetByteArrayAsync("/api/public/brand/logo/on-light"));
        var startedArtwork = await client.GetAsync($"/api/public/brand/media/{Sha(Png(240))}");
        var kit = await client.GetFromJsonAsync<JsonElement>("/api/public/brand");
        var tokens = await client.GetStringAsync("/api/public/brand/tokens.css");
        await client.PostAsJsonAsync($"/api/brand/appearances/{appearance.GetProperty("id").GetString()}/cancel", new { });
        var after = Sha(await client.GetByteArrayAsync("/api/public/brand/logo/on-light"));

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, overlapping.StatusCode);
        Assert.Equal(Sha(Png(240)), during);
        Assert.Equal(HttpStatusCode.OK, startedArtwork.StatusCode);
        Assert.True(kit.GetProperty("logos").GetProperty("on-light").GetProperty("fromAppearance").GetBoolean());
        Assert.Equal("Navro‘z", kit.GetProperty("appearance").GetProperty("name").GetString());
        Assert.Contains("--ocharo-accent: #1f7a4d;", tokens);
        Assert.Equal(Sha(Png(60)), after);
    }

    [Fact]
    public async Task Future_appearance_artwork_stays_private_and_caps_public_cache_lifetimes()
    {
        var client = factory.CreateClient();
        using var form = new MultipartFormDataContent();
        var artwork = Png(170);
        var file = new ByteArrayContent(artwork);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "file", "collection-launch.png");
        var upload = await (await client.PostAsync("/api/brand/logos/on-light", form)).Content.ReadFromJsonAsync<JsonElement>();
        var startsAt = DateTimeOffset.UtcNow.AddSeconds(40);

        var saved = await client.PostAsJsonAsync("/api/brand/appearances", new
        {
            name = "Collection launch", startsAt, endsAt = startsAt.AddDays(1),
            overrides = new Dictionary<string, string> { ["on-light"] = upload.GetProperty("id").GetString()! },
        });
        var embargoed = await client.GetAsync($"/api/public/brand/media/{Sha(artwork)}");
        var plainLink = await client.PostAsJsonAsync("/api/brand/appearances", new
        {
            name = "Bad link", startsAt = startsAt.AddDays(2), endsAt = startsAt.AddDays(3), linkLabel = "Open", linkUrl = "http://example.com/",
        });
        var badgeOnly = await client.PostAsJsonAsync("/api/brand/appearances", new
        {
            name = "Open studio", startsAt = DateTimeOffset.UtcNow.AddMinutes(-1), endsAt = startsAt.AddSeconds(-1),
            linkLabel = "Open studio", linkUrl = "https://t.me/Ocharo_Style",
        });
        var kit = await client.GetAsync("/api/public/brand");
        var logo = await client.GetAsync("/api/public/brand/logo/on-light");

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, embargoed.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, plainLink.StatusCode);
        Assert.Equal(HttpStatusCode.OK, badgeOnly.StatusCode);
        var active = (await kit.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("appearance");
        Assert.Equal("Open studio", active.GetProperty("linkLabel").GetString());
        Assert.Equal("https://t.me/Ocharo_Style", active.GetProperty("linkUrl").GetString());
        Assert.DoesNotContain(Sha(artwork), await kit.Content.ReadAsStringAsync());
        Assert.InRange(logo.Headers.CacheControl!.MaxAge!.Value.TotalSeconds, 1, 40);
        Assert.InRange(kit.Headers.CacheControl!.MaxAge!.Value.TotalSeconds, 1, 40);
    }

    [Fact]
    public async Task Identity_edits_are_validated_revision_checked_and_cross_origin_safe()
    {
        var client = factory.CreateClient();
        var draft = await client.GetFromJsonAsync<JsonElement>("/api/brand/draft");
        var revision = draft.GetProperty("revision").GetInt32();
        var identity = JsonSerializer.Deserialize<Dictionary<string, object?>>(draft.GetProperty("draft").GetProperty("identity").GetRawText())!;
        identity["palette"] = new[] { new { key = "accent", name = "Accent", hex = "red", role = "accent" } };

        var invalid = await client.PutAsJsonAsync("/api/brand/identity", new { expectedRevision = revision, identity });
        identity["palette"] = new[] { new { key = "accent", name = "Accent", hex = "#aa3322", role = "accent" } };
        var saved = await client.PutAsJsonAsync("/api/brand/identity", new { expectedRevision = revision, identity });
        var stale = await client.PutAsJsonAsync("/api/brand/identity", new { expectedRevision = revision, identity });
        using var crossOrigin = new HttpRequestMessage(HttpMethod.Put, "/api/brand/identity") { Content = JsonContent.Create(new { expectedRevision = revision + 1, identity }) };
        crossOrigin.Headers.Add("Origin", "https://evil.example");
        var rejected = await client.SendAsync(crossOrigin);
        var publicKit = await client.GetAsync("/api/public/brand");

        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, rejected.StatusCode);
        Assert.Equal("*", publicKit.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }
}
