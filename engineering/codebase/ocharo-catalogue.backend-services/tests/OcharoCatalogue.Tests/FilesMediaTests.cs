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
                ["Files:BaseUrl"] = TestFiles.Url,
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

    [Fact]
    public async Task A_release_artifact_prepared_before_files_reads_from_sqlite_until_the_migration_fills_its_id()
    {
        var bytes = CatalogueHarness.Png(85);
        var sha = Sha(bytes);
        var local = await ServicesAsync(null);
        var stored = await Scope(local).GetRequiredService<IReleaseAssetStore>()
            .StoreAsync([new ReleaseArtifact(sha, "image/png", "ocharo-spring-1.png", bytes)], default);
        await using (var scope = local.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OcharoCatalogueDbContext>();
            db.CatalogueReleaseAssets.AddRange(stored.Rows);
            await db.SaveChangesAsync();
        }

        var unmigrated = await ServicesAsync(new TestFiles());
        var before = await Scope(unmigrated).GetRequiredService<IReleaseAssetStore>().LinkAsync(sha, default);

        var migrated = new TestFiles();
        var moved = migrated.Add(bytes, "catalogue", new FileReference("catalogue.release", sha));
        var after = await ServicesAsync(migrated);
        await Scope(after).GetRequiredService<FilesMediaBackfill>().RunAsync(default);
        var designs = Scope(after).GetRequiredService<IDesignRepository>();
        var link = await Scope(after).GetRequiredService<IReleaseAssetStore>().LinkAsync(sha, default);
        var file = await designs.ReleaseFileAsync(sha, default);

        Assert.Single(stored.Rows);
        Assert.Empty(stored.Files);
        Assert.Null(before);
        Assert.Equal(moved.Id.ToString(), file?.FileId);
        Assert.Equal("image/png", file?.ContentType);
        Assert.Equal(TestFiles.Link(moved.Id, null, FileDisposition.Attachment), link);
        Assert.NotNull(await designs.AssetAsync(sha, default));
    }

    [Fact]
    public async Task A_file_deleted_in_files_fails_the_release_check_and_a_new_upload_brings_it_back()
    {
        var files = new TestFiles();
        var services = await ServicesAsync(files);
        var media = Scope(services).GetRequiredService<IMediaStore>();
        var bytes = CatalogueHarness.Png(71);
        var sha = Sha(bytes);
        var stored = await media.StoreAsync(bytes, default);
        FileId.TryParse(stored.FileId, out var id);

        await files.DeleteAsync(id);
        var deleted = await media.CheckAsync(sha, default);
        var restored = await media.StoreAsync(bytes, default);
        var afterRestore = await media.CheckAsync(sha, default);

        await files.DeleteAsync(id);
        files.RestoreFails = true;
        var uploadsBefore = files.Uploads;
        var replaced = await media.StoreAsync(bytes, default);
        var afterReplace = await media.CheckAsync(sha, default);
        var record = await Scope(services).GetRequiredService<ICatalogueRepository>().FindMediaAsync(sha, default);

        Assert.Equal(MediaCheck.Missing, deleted);
        Assert.Equal(stored.FileId, restored.FileId);
        Assert.Equal(MediaCheck.Ok, afterRestore);
        Assert.Equal(uploadsBefore + 1, files.Uploads);
        Assert.NotEqual(stored.FileId, replaced.FileId);
        Assert.Equal(replaced.FileId, record?.FileId);
        Assert.Equal(MediaCheck.Ok, afterReplace);
    }

    private async Task<ServiceProvider> ServicesAsync(TestFiles? files)
    {
        Directory.CreateDirectory(root);
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:OcharoCatalogue"] = $"Data Source={Path.Combine(root, "catalogue.db")};Pooling=False",
            ["Catalogue:DataRoot"] = Path.Combine(root, "data"),
            ["Files:BaseUrl"] = files is null ? null : TestFiles.Url,
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

    private static void Use(IServiceCollection services, TestFiles files) => files.Register(services);

    private static string Sha(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
