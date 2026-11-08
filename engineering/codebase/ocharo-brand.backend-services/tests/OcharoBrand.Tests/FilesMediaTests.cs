using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Files.References;
using WoW.Two.Sdk.Beta.Be.Files.Store;
using WoW.Two.Sdk.Beta.Be.Files.Uploads;
using WoW.Two.Sdk.Beta.Be.Files.Urls;

namespace OcharoBrand.Tests;

/// <summary>Brand on WoW2 Files (<c>Files:BaseUrl</c> set): the seed and uploads land in Files, every media route redirects
/// to a short-lived Files link, and a file stored before Files reads its local bytes until the migration fills its id.
/// Files itself is <see cref="TestFiles"/>, an in-memory broker set; the HTTP client is the be sdk's.</summary>
public sealed class FilesMediaTests : IAsyncDisposable
{
    private const string FilesUrl = "https://files.example.test/";
    private readonly string root = Path.Combine(Path.GetTempPath(), "ocharo-brand-files-tests", Guid.NewGuid().ToString("N"));
    private readonly string logos;
    private readonly List<WebApplicationFactory<Program>> factories = [];

    public FilesMediaTests()
    {
        logos = Path.Combine(root, "logos");
        Directory.CreateDirectory(logos);
        File.WriteAllBytes(Path.Combine(logos, "och-logo-approved-ivory-v1.png"), Png(10));
        File.WriteAllBytes(Path.Combine(logos, "och-logo-black-transparent-v1.png"), Png(60));
        File.WriteAllBytes(Path.Combine(logos, "och-logo-white-transparent-v1.png"), Png(110));
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var factory in factories)
            await factory.DisposeAsync();
        try { Directory.Delete(root, true); } catch (IOException) { }
    }

    [Fact]
    public async Task The_seed_lands_in_files_and_every_media_route_redirects_to_a_link()
    {
        var files = new TestFiles();
        var factory = Host(files);
        using var anonymous = BrandTestIdentity.Anonymous(factory);
        using var owner = await BrandTestIdentity.SignedIn(factory, BrandTestIdentity.Owner);
        var sha = Sha(Png(10));

        var logo = await anonymous.GetAsync("/api/public/brand/logo/social");
        var media = await anonymous.GetAsync($"/api/public/brand/media/{sha}");
        var preview = await anonymous.GetAsync($"/api/public/brand/media/{sha}/preview");
        var admin = await owner.GetAsync($"/api/brand/media/{sha}");

        Assert.Equal(3, files.Count);
        var stored = files.BySha(sha);
        Assert.Equal("brand", stored.AppId);
        Assert.Contains(new FileReference("brand.media", sha), files.ReferencesOf(stored.Id));
        Assert.Equal(HttpStatusCode.Redirect, logo.StatusCode);
        Assert.Equal(TestFiles.Link(stored.Id, null), logo.Headers.Location);
        Assert.Equal(HttpStatusCode.Redirect, media.StatusCode);
        Assert.True(media.Headers.CacheControl?.Public);
        Assert.Equal(TimeSpan.FromMinutes(5), media.Headers.CacheControl?.MaxAge);
        Assert.Equal(TestFiles.Link(stored.Id, 1200), preview.Headers.Location);
        Assert.Equal(HttpStatusCode.Redirect, admin.StatusCode);
        Assert.True(admin.Headers.CacheControl?.Private);
        Assert.Equal(TimeSpan.FromMinutes(5), admin.Headers.CacheControl?.MaxAge);
        Assert.False(Directory.Exists(Path.Combine(root, "data", "blobs", "media")));
    }

    [Fact]
    public async Task A_file_stored_before_files_reads_locally_until_the_migration_fills_its_id()
    {
        var sha = Sha(Png(10));
        var local = LocalHost();
        using (var client = BrandTestIdentity.Anonymous(local))
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/public/brand/media/{sha}")).StatusCode);
        await local.DisposeAsync();
        factories.Remove(local);

        var unmigrated = Host(new TestFiles());
        using (var client = BrandTestIdentity.Anonymous(unmigrated))
        {
            var before = await client.GetAsync($"/api/public/brand/media/{sha}");
            Assert.Equal(HttpStatusCode.OK, before.StatusCode);
            Assert.Equal(sha, Sha(await before.Content.ReadAsByteArrayAsync()));
        }
        await unmigrated.DisposeAsync();
        factories.Remove(unmigrated);

        var migrated = new TestFiles();
        var moved = migrated.Add(Png(10), "brand", new FileReference("brand.media", sha));
        using var after = BrandTestIdentity.Anonymous(Host(migrated));
        var response = await after.GetAsync($"/api/public/brand/media/{sha}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(TestFiles.Link(moved.Id, null), response.Headers.Location);
    }

    [Fact]
    public async Task The_files_client_takes_brands_identity_client_and_an_unreachable_files_spares_the_boot()
    {
        // A hosted Brand has no seed folder; a fresh store with one would upload at boot and stop on an unreachable Files.
        var factory = Factory(new()
        {
            ["Files:BaseUrl"] = FilesUrl,
            ["Identity:Client:ClientSecret"] = "test-secret",
            ["Brand:SeedLogoRoot"] = Path.Combine(root, "no-seed"),
        }, _ => { });
        using var client = BrandTestIdentity.Anonymous(factory);

        var health = await client.GetAsync("/health");
        var options = factory.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<WoW.Two.Sdk.Beta.Be.Files.Client.FilesClientOptions>>().Value;

        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        Assert.Equal(new Uri(FilesUrl), options.BaseAddress);
        Assert.Equal(new Uri("https://identity.example.test/connect/token"), options.Identity?.TokenEndpoint);
        Assert.Equal("brand", options.Identity?.ClientId);
    }

    private WebApplicationFactory<Program> Host(TestFiles files) => Factory(new() { ["Files:BaseUrl"] = FilesUrl }, services =>
    {
        services.AddSingleton(files);
        services.AddSingleton<IFileUploadsBroker>(files);
        services.AddSingleton<IFileStoreBroker>(files);
        services.AddSingleton<IFileReferencesBroker>(files);
        services.AddSingleton<IFileUrlsBroker>(files);
    });

    private WebApplicationFactory<Program> LocalHost() => Factory([], _ => { });

    private WebApplicationFactory<Program> Factory(Dictionary<string, string?> extra, Action<IServiceCollection> services)
    {
        var settings = new Dictionary<string, string?>(BrandTestIdentity.Settings(root))
        {
            ["ConnectionStrings:OcharoBrand"] = $"Data Source={Path.Combine(root, "brand.db")};Pooling=False",
            ["Brand:DataRoot"] = Path.Combine(root, "data"),
            ["Brand:SeedLogoRoot"] = logos,
        };
        foreach (var (key, value) in extra)
            settings[key] = value;
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(settings));
            builder.ConfigureServices(BrandTestIdentity.Use);
            builder.ConfigureTestServices(services);
        });
        factories.Add(factory);
        return factory;
    }

    private static byte[] Png(byte shade)
    {
        using var bitmap = new SKBitmap(80, 40, SKColorType.Rgba8888, SKAlphaType.Premul);
        bitmap.Erase(new SKColor(shade, (byte)(255 - shade), 40, 255));
        using var image = SKImage.FromBitmap(bitmap);
        return image.Encode(SKEncodedImageFormat.Png, 100).ToArray();
    }

    private static string Sha(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    /// <summary>An in-memory Files tenant: uploads keep their bytes, idempotency key and app; references attach; links
    /// name the file and the asked width.</summary>
    private sealed class TestFiles : IFileUploadsBroker, IFileStoreBroker, IFileReferencesBroker, IFileUrlsBroker
    {
        private readonly ConcurrentDictionary<FileId, FileModel> files = new();
        private readonly ConcurrentDictionary<string, FileId> keys = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<FileId, ConcurrentBag<FileReference>> references = new();

        public int Count => files.Count;

        public static Uri Link(FileId id, int? width) => new($"{FilesUrl}f/{id}?w={width}");

        public FileModel BySha(string sha) => files.Values.Single(file => file.Sha256 == sha);

        public IReadOnlyCollection<FileReference> ReferencesOf(FileId id) => references.TryGetValue(id, out var bag) ? bag : [];

        /// <summary>Holds a file as the migration would leave it: moved, with its reference attached.</summary>
        public FileModel Add(byte[] bytes, string appId, FileReference reference)
        {
            var file = Model(bytes, $"{appId}-moved", appId);
            files[file.Id] = file;
            references.GetOrAdd(file.Id, _ => []).Add(reference);
            return file;
        }

        public async Task<Result<FileModel>> UploadAsync(Stream content, UploadSpec spec, CancellationToken cancellationToken = default)
        {
            if (spec.IdempotencyKey is { } key && keys.TryGetValue(key, out var first))
                return Result<FileModel>.Ok(files[first]);
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);
            var file = Model(buffer.ToArray(), spec.Name, spec.AppId);
            files[file.Id] = file;
            if (spec.IdempotencyKey is { } idempotency)
                keys[idempotency] = file.Id;
            return Result<FileModel>.Ok(file);
        }

        public Task<Result> AttachAsync(FileId id, FileReference reference, CancellationToken cancellationToken = default)
        {
            references.GetOrAdd(id, _ => []).Add(reference);
            return Task.FromResult(Result.Ok());
        }

        public Task<Result<SignedFileUrl>> IssueAsync(FileId id, UrlSpec spec, CancellationToken cancellationToken = default) =>
            Task.FromResult(files.ContainsKey(id)
                ? Result<SignedFileUrl>.Ok(new SignedFileUrl(Link(id, spec.Width), DateTimeOffset.UtcNow + spec.Lifetime))
                : Result<SignedFileUrl>.Fail(WoW.Two.Sdk.Beta.Be.Foundation.Errors.AppErrorFactory.NotFound("No such file.")));

        public Task<Result<FilePage>> ListAsync(FileQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<FilePage>.Ok(new FilePage(files.Values.Where(file => query.AppId is null || file.AppId == query.AppId).ToList(), null)));

        public Task<Result<FileModel>> GetAsync(FileId id, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Result<FileContent>> OpenReadAsync(FileId id, ByteRange? range = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Result<FileModel>> UpdateAsync(FileId id, FilePatch patch, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Result<FileModel>> DeleteAsync(FileId id, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Result<FileModel>> RestoreAsync(FileId id, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Result> DetachAsync(FileId id, FileReference reference, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Result<IReadOnlyList<FileReference>>> ListAsync(FileId id, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Result<Uri>> GetPublicUrlAsync(FileId id, int? width = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        private static FileModel Model(byte[] bytes, string name, string? appId) => new()
        {
            Id = FileId.New(),
            TenantId = "ocharo",
            AppId = appId,
            Name = name,
            ContentType = "image/png",
            Kind = FileKind.Image,
            Size = bytes.LongLength,
            Sha256 = Sha(bytes),
            Visibility = FileVisibility.Private,
            State = FileState.Ready,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
    }
}
