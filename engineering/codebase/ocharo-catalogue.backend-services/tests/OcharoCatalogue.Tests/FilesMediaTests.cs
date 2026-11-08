using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OcharoCatalogue.Api.Configurations;
using OcharoCatalogue.Application.Catalogue.Repositories;
using OcharoCatalogue.Application.Catalogue.Services;
using OcharoCatalogue.Infrastructure.Catalogue;
using OcharoCatalogue.Persistence;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Files.References;
using WoW.Two.Sdk.Beta.Be.Files.Store;
using WoW.Two.Sdk.Beta.Be.Files.Uploads;
using WoW.Two.Sdk.Beta.Be.Files.Urls;

namespace OcharoCatalogue.Tests;

/// <summary>Catalogue on WoW2 Files (<c>Files:BaseUrl</c> set): product images land in Files, the media route redirects to
/// a short-lived Files link, the release check asks Files, and an image stored before Files reads its local bytes until
/// the migration fills its id. Files itself is <see cref="TestFiles"/>, an in-memory broker set.</summary>
public sealed class FilesMediaTests : IAsyncDisposable
{
    private const string FilesUrl = "https://files.example.test/";
    private readonly string root = Path.Combine(Path.GetTempPath(), "ocharo-catalogue-files-tests", Guid.NewGuid().ToString("N"));
    private readonly List<IAsyncDisposable> hosts = [];

    public async ValueTask DisposeAsync()
    {
        foreach (var host in hosts)
            await host.DisposeAsync();
        try { Directory.Delete(root, true); } catch (IOException) { }
    }

    [Fact]
    public async Task An_image_lands_in_files_once_and_opens_as_a_link_its_check_asks_files()
    {
        var files = new TestFiles();
        var services = await ServicesAsync(files);
        var media = Scope(services).GetRequiredService<IMediaStore>();
        var bytes = CatalogueHarness.Png(70);
        var sha = Sha(bytes);

        var stored = await media.StoreAsync(bytes, default);
        var again = await media.StoreAsync(bytes, default);
        var file = files.Model(stored.FileId!);
        var original = await media.OpenAsync(sha, false, default);
        var preview = await media.OpenAsync(sha, true, default);
        var healthy = await media.CheckAsync(sha, default);
        files.Corrupt(sha);
        var corrupt = await media.CheckAsync(sha, default);
        files.Remove(sha);
        var missing = await media.CheckAsync(sha, default);

        Assert.IsType<FilesMediaStore>(media);
        Assert.Equal(1, files.Uploads);
        Assert.Equal(stored.FileId, again.FileId);
        Assert.Equal("catalogue", file.AppId);
        Assert.Contains(new FileReference("catalogue.media", sha), files.ReferencesOf(file.Id));
        Assert.True(MediaPreview.Exists(stored));
        Assert.Equal(TestFiles.Link(file.Id, null), original?.Location);
        Assert.Equal(TestFiles.Link(file.Id, 900), preview?.Location);
        Assert.Equal("image/webp", preview?.ContentType);
        Assert.Equal(MediaCheck.Ok, healthy);
        Assert.Equal(MediaCheck.Corrupt, corrupt);
        Assert.Equal(MediaCheck.Missing, missing);
        Assert.False(Directory.Exists(Path.Combine(root, "data", "blobs", "media")));
    }

    [Fact]
    public async Task An_image_stored_before_files_reads_locally_until_the_migration_fills_its_id()
    {
        var bytes = CatalogueHarness.Png(80);
        var sha = Sha(bytes);
        var local = await ServicesAsync(null);
        await Scope(local).GetRequiredService<IMediaStore>().StoreAsync(bytes, default);

        var unmigrated = await ServicesAsync(new TestFiles());
        var before = await Scope(unmigrated).GetRequiredService<IMediaStore>().OpenAsync(sha, false, default);
        var beforeCheck = await Scope(unmigrated).GetRequiredService<IMediaStore>().CheckAsync(sha, default);

        var migrated = new TestFiles();
        var moved = migrated.Add(bytes, "catalogue", new FileReference("catalogue.media", sha));
        var after = await ServicesAsync(migrated);
        await Scope(after).GetRequiredService<FilesMediaBackfill>().RunAsync(default);
        var opened = await Scope(after).GetRequiredService<IMediaStore>().OpenAsync(sha, false, default);
        var record = await Scope(after).GetRequiredService<ICatalogueRepository>().FindMediaAsync(sha, default);

        Assert.NotNull(before?.Content);
        await using (before!.Content)
            Assert.Equal(sha, Convert.ToHexStringLower(await SHA256.HashDataAsync(before.Content!)));
        Assert.Equal(MediaCheck.Ok, beforeCheck);
        Assert.Equal(moved.Id.ToString(), record?.FileId);
        Assert.Equal(TestFiles.Link(moved.Id, null), opened?.Location);
    }

    [Fact]
    public async Task The_media_route_redirects_a_member_to_the_link_and_keeps_deciding_each_time()
    {
        var files = new TestFiles();
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseCatalogueSignIn();
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:OcharoCatalogue"] = $"Data Source={Path.Combine(root, "web.db")};Pooling=False",
                ["Catalogue:DataRoot"] = Path.Combine(root, "web"),
                ["Catalogue:SourceRoot"] = null,
                ["Files:BaseUrl"] = FilesUrl,
                ["DataProtection:KeysPath"] = Path.Combine(root, "web", "keys"),
            }));
            builder.ConfigureTestServices(services => Use(services, files));
        });
        hosts.Add(factory);
        var stored = await factory.Services.CreateScope().ServiceProvider.GetRequiredService<IMediaStore>().StoreAsync(CatalogueHarness.Png(90), default);
        using var member = CatalogueSignIn.CreateClient(factory, CatalogueSignIn.Owner);
        using var outsider = CatalogueSignIn.CreateClient(factory, CatalogueSignIn.Outsider);

        var response = await member.GetAsync($"/api/catalogue/media/{stored.Sha256}/preview");
        var hidden = await outsider.GetAsync($"/api/catalogue/media/{stored.Sha256}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(TestFiles.Link(FileId.TryParse(stored.FileId, out var id) ? id : default, 900), response.Headers.Location);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
    }

    private async Task<ServiceProvider> ServicesAsync(TestFiles? files)
    {
        Directory.CreateDirectory(root);
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:OcharoCatalogue"] = $"Data Source={Path.Combine(root, "catalogue.db")};Pooling=False",
            ["Catalogue:DataRoot"] = Path.Combine(root, "data"),
            ["Files:BaseUrl"] = files is null ? null : FilesUrl,
        };
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(HostConfiguration).Assembly.GetName().Name,
            ContentRootPath = root,
            EnvironmentName = "Testing",
        });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(settings);
        builder.Configure();
        if (files is not null)
            Use(builder.Services, files);
        var services = builder.Services.BuildServiceProvider();
        hosts.Add(services);
        await services.InitializeDatabaseAsync();
        return services;
    }

    private static IServiceProvider Scope(IServiceProvider services) => services.CreateScope().ServiceProvider;

    private static void Use(IServiceCollection services, TestFiles files)
    {
        services.AddSingleton(files);
        services.AddSingleton<IFileUploadsBroker>(files);
        services.AddSingleton<IFileStoreBroker>(files);
        services.AddSingleton<IFileReferencesBroker>(files);
        services.AddSingleton<IFileUrlsBroker>(files);
    }

    private static string Sha(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    /// <summary>An in-memory Files tenant: uploads keep their bytes, idempotency key and app; references attach; links
    /// name the file and the asked width; a file can be corrupted or removed to exercise the release check.</summary>
    private sealed class TestFiles : IFileUploadsBroker, IFileStoreBroker, IFileReferencesBroker, IFileUrlsBroker
    {
        private readonly ConcurrentDictionary<FileId, FileModel> files = new();
        private readonly ConcurrentDictionary<string, FileId> keys = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<FileId, ConcurrentBag<FileReference>> references = new();

        public int Uploads { get; private set; }

        public static Uri Link(FileId id, int? width) => new($"{FilesUrl}f/{id}?w={width}");

        public FileModel Model(string id) => files[FileId.TryParse(id, out var parsed) ? parsed : default];

        public IReadOnlyCollection<FileReference> ReferencesOf(FileId id) => references.TryGetValue(id, out var bag) ? bag : [];

        public void Corrupt(string sha)
        {
            var file = files.Values.Single(model => model.Sha256 == sha);
            files[file.Id] = file with { Sha256 = new string('0', 64) };
        }

        public void Remove(string sha) => files.TryRemove(files.Values.Single(model => model.Sha256 == new string('0', 64) || model.Sha256 == sha).Id, out _);

        /// <summary>Holds a file as the migration would leave it: moved, with its reference attached.</summary>
        public FileModel Add(byte[] bytes, string appId, FileReference reference)
        {
            var file = NewModel(bytes, $"{appId}-moved", appId);
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
            var file = NewModel(buffer.ToArray(), spec.Name, spec.AppId);
            files[file.Id] = file;
            Uploads++;
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
                : Result<SignedFileUrl>.Fail(AppErrorFactory.NotFound("No such file.")));

        public Task<Result<FileModel>> GetAsync(FileId id, CancellationToken cancellationToken = default) =>
            Task.FromResult(files.TryGetValue(id, out var file)
                ? Result<FileModel>.Ok(file)
                : Result<FileModel>.Fail(AppErrorFactory.NotFound("No such file.")));

        public Task<Result<FilePage>> ListAsync(FileQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<FilePage>.Ok(new FilePage(files.Values.Where(file => query.AppId is null || file.AppId == query.AppId).ToList(), null)));

        public Task<Result<FileContent>> OpenReadAsync(FileId id, ByteRange? range = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Result<FileModel>> UpdateAsync(FileId id, FilePatch patch, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Result<FileModel>> DeleteAsync(FileId id, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Result<FileModel>> RestoreAsync(FileId id, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Result> DetachAsync(FileId id, FileReference reference, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Result<IReadOnlyList<FileReference>>> ListAsync(FileId id, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Result<Uri>> GetPublicUrlAsync(FileId id, int? width = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        private static FileModel NewModel(byte[] bytes, string name, string? appId) => new()
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
