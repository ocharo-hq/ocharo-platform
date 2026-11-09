using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Beta.Be.Files.Client;
using WoW.Two.Sdk.Beta.Be.Files.References;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;

namespace OcharoBrand.Tests.E2E;

/// <summary>Brand on WoW2 Files (<c>Files:BaseUrl</c> set): the seed and uploads land in Files, every media route redirects
/// to a short-lived Files link, and a file stored before Files reads its local bytes until the migration fills its id.
/// Files itself is <see cref="TestFiles"/>, an in-memory broker set; the HTTP client is the be sdk's.</summary>
public sealed class FilesMediaTests : IAsyncDisposable
{
    private readonly FilesWorkspace workspace = new();

    public async ValueTask DisposeAsync() => await workspace.DisposeAsync();

    [Fact]
    public async Task Upload_ShouldAnswer502Or503_WhenFilesRefusesOrIsUnreachable()
    {
        var files = new TestFiles();
        var factory = workspace.Host(files);
        using var owner = await BrandTestIdentity.SignedIn(factory, BrandTestIdentity.Owner);

        files.UploadRefusal = AppErrorFactory.Forbidden("Not this app.");
        using var refused = await UploadAsync(owner, TestArtwork.Png(200));
        files.UploadRefusal = AppErrorFactory.ExternalUnavailable("Down.");
        using var unreachable = await UploadAsync(owner, TestArtwork.Png(201));

        refused.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        (await refused.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("brandCode").GetString().Should().Be("files_refused");
        unreachable.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task MediaRoutes_ShouldRedirectToFilesLink_WhenSeedLandedInFiles()
    {
        var files = new TestFiles();
        var factory = workspace.Host(files);
        using var anonymous = BrandTestIdentity.Anonymous(factory);
        using var owner = await BrandTestIdentity.SignedIn(factory, BrandTestIdentity.Owner);
        var sha = TestArtwork.Sha(TestArtwork.Png(10));

        var logo = await anonymous.GetAsync("/api/public/brand/logo/social");
        var media = await anonymous.GetAsync($"/api/public/brand/media/{sha}");
        var preview = await anonymous.GetAsync($"/api/public/brand/media/{sha}/preview");
        var admin = await owner.GetAsync($"/api/brand/media/{sha}");
        var kit = await anonymous.GetFromJsonAsync<JsonElement>("/api/public/brand");

        files.Count.Should().Be(3);
        var stored = files.BySha(sha);
        kit.GetProperty("logos").GetProperty("social").GetProperty("fileId").GetString().Should().Be(stored.Id.ToString());
        stored.AppId.Should().Be("brand");
        files.ReferencesOf(stored.Id).Should().Contain(new FileReference("brand.media", sha));
        logo.StatusCode.Should().Be(HttpStatusCode.Redirect);
        logo.Headers.Location.Should().Be(TestFiles.Link(stored.Id, null));
        media.StatusCode.Should().Be(HttpStatusCode.Redirect);
        media.Headers.CacheControl?.Public.Should().BeTrue();
        media.Headers.CacheControl?.MaxAge.Should().Be(TimeSpan.FromMinutes(5));
        preview.Headers.Location.Should().Be(TestFiles.Link(stored.Id, 1200));
        admin.StatusCode.Should().Be(HttpStatusCode.Redirect);
        admin.Headers.CacheControl?.Private.Should().BeTrue();
        admin.Headers.CacheControl?.MaxAge.Should().Be(TimeSpan.FromMinutes(5));
        Directory.Exists(Path.Combine(workspace.Root, "data", "blobs", "media")).Should().BeFalse();
    }

    [Fact]
    public async Task MediaRoute_ShouldReadLocalUntilMigrated_WhenFileWasStoredBeforeFiles()
    {
        var sha = TestArtwork.Sha(TestArtwork.Png(10));
        var local = workspace.LocalHost();
        using (var client = BrandTestIdentity.Anonymous(local))
            (await client.GetAsync($"/api/public/brand/media/{sha}")).StatusCode.Should().Be(HttpStatusCode.OK);
        await workspace.StopAsync(local);

        var unmigrated = workspace.Host(new TestFiles());
        using (var client = BrandTestIdentity.Anonymous(unmigrated))
        {
            var before = await client.GetAsync($"/api/public/brand/media/{sha}");
            before.StatusCode.Should().Be(HttpStatusCode.OK);
            TestArtwork.Sha(await before.Content.ReadAsByteArrayAsync()).Should().Be(sha);
        }
        await workspace.StopAsync(unmigrated);

        var migrated = new TestFiles();
        var moved = migrated.Add(TestArtwork.Png(10), "brand", new FileReference("brand.media", sha));
        using var after = BrandTestIdentity.Anonymous(workspace.Host(migrated));
        var response = await after.GetAsync($"/api/public/brand/media/{sha}");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location.Should().Be(TestFiles.Link(moved.Id, null));
    }

    [Fact]
    public async Task PublicFeed_ShouldOmitFileId_WhenLogoIsStillLocal()
    {
        var local = workspace.LocalHost();
        using (var client = BrandTestIdentity.Anonymous(local))
        {
            var before = await client.GetFromJsonAsync<JsonElement>("/api/public/brand");
            // The wire contract omits a null, so a logo still only in the local blob folder carries no file id.
            Logos(before).Should().AllSatisfy(logo => logo.TryGetProperty("fileId", out _).Should().BeFalse());
        }
        await workspace.StopAsync(local);

        var migrated = new TestFiles();
        var moved = migrated.Add(TestArtwork.Png(10), "brand", new FileReference("brand.media", TestArtwork.Sha(TestArtwork.Png(10))));
        using var anonymous = BrandTestIdentity.Anonymous(workspace.Host(migrated));
        var kit = await anonymous.GetFromJsonAsync<JsonElement>("/api/public/brand");

        kit.GetProperty("logos").GetProperty("master").GetProperty("fileId").GetString().Should().Be(moved.Id.ToString());
        kit.GetProperty("logos").GetProperty("social").GetProperty("fileId").GetString().Should().Be(moved.Id.ToString());
        kit.GetProperty("logos").GetProperty("on-light").TryGetProperty("fileId", out _).Should().BeFalse();
        Logos(kit).Where(logo => logo.TryGetProperty("fileId", out _))
            .Should().AllSatisfy(logo => logo.GetProperty("sha256").GetString().Should().Be(moved.Sha256));
    }

    [Fact]
    public async Task FilesClient_ShouldTakeIdentityClientAndSpareBoot_WhenFilesIsUnreachable()
    {
        // A hosted Brand has no seed folder; a fresh store with one would upload at boot and stop on an unreachable Files.
        var factory = workspace.Factory(new()
        {
            ["Files:BaseUrl"] = TestFiles.BaseUrl,
            ["Identity:Client:ClientSecret"] = "test-secret",
            ["Brand:SeedLogoRoot"] = Path.Combine(workspace.Root, "no-seed"),
        }, _ => { });
        using var client = BrandTestIdentity.Anonymous(factory);

        var health = await client.GetAsync("/health");
        var options = factory.Services.GetRequiredService<IOptions<FilesClientOptions>>().Value;

        health.StatusCode.Should().Be(HttpStatusCode.OK);
        options.BaseAddress.Should().Be(new Uri(TestFiles.BaseUrl));
        options.Identity?.TokenEndpoint.Should().Be(new Uri("https://identity.example.test/connect/token"));
        options.Identity?.ClientId.Should().Be("brand");
    }

    private static async Task<HttpResponseMessage> UploadAsync(HttpClient client, byte[] bytes)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "file", "upload.png");
        return await client.PostAsync("/api/brand/logos/on-light", form);
    }

    private static IEnumerable<JsonElement> Logos(JsonElement kit) => kit.GetProperty("logos").EnumerateObject().Select(slot => slot.Value);
}
