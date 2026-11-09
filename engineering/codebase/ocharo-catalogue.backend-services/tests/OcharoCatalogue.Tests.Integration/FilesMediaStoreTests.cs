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
using OcharoCatalogue.Persistence;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Files.References;
using WoW.Two.Sdk.Beta.Be.Files.Store;
using WoW.Two.Sdk.Beta.Be.Files.Uploads;
using WoW.Two.Sdk.Beta.Be.Files.Urls;
using OcharoCatalogue.Application.Catalogue.Core.Repositories;
using OcharoCatalogue.Application.Catalogue.Publishing.Models;
using OcharoCatalogue.Application.Catalogue.Publishing.Services;
using OcharoCatalogue.Application.Catalogue.Media.Services;
using OcharoCatalogue.Application.Catalogue.Media.Enums;
using OcharoCatalogue.Infrastructure.Catalogue.Media.Mappers;
using OcharoCatalogue.Infrastructure.Catalogue.Media.FoundationServices;
using OcharoCatalogue.Infrastructure.Catalogue.Media.Stores;
using WoW.Two.Sdk.Beta.Be.Mediator;

namespace OcharoCatalogue.Tests.Integration;

/// <summary>Catalogue's stores on WoW2 Files (<c>Files:BaseUrl</c> set): product images land in Files, the release check asks
/// Files, and an image or release artifact stored before Files reads its local bytes until the migration fills its id.
/// Files itself is <see cref="TestFiles"/>, an in-memory broker set.</summary>
public sealed class FilesMediaStoreTests : IAsyncDisposable
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
    public async Task MediaStore_ShouldUploadOnceAndOpenAsLink_WhenOnFiles()
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

        media.Should().BeOfType<FilesMediaStore>();
        files.Uploads.Should().Be(1);
        again.FileId.Should().Be(stored.FileId);
        file.AppId.Should().Be("catalogue");
        files.ReferencesOf(file.Id).Should().Contain(new FileReference("catalogue.media", sha));
        MediaPreview.Exists(stored).Should().BeTrue();
        original?.Location.Should().Be(TestFiles.Link(file.Id, null));
        preview?.Location.Should().Be(TestFiles.Link(file.Id, 900));
        preview?.ContentType.Should().Be("image/webp");
        healthy.Should().Be(MediaCheck.Ok);
        corrupt.Should().Be(MediaCheck.Corrupt);
        missing.Should().Be(MediaCheck.Missing);
        Directory.Exists(Path.Combine(root, "data", "blobs", "media")).Should().BeFalse();
    }

    [Fact]
    public async Task MediaStore_ShouldReadLocallyUntilMigrated_WhenImageWasStoredBeforeFiles()
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
        await Scope(after).GetRequiredService<FilesMediaBackfillService>().RunAsync(default);
        var opened = await Scope(after).GetRequiredService<IMediaStore>().OpenAsync(sha, false, default);
        var record = await Scope(after).GetRequiredService<ICatalogueRepository>().FindMediaAsync(sha, default);

        before?.Content.Should().NotBeNull();
        await using (before!.Content)
            Convert.ToHexStringLower(await SHA256.HashDataAsync(before.Content!)).Should().Be(sha);
        beforeCheck.Should().Be(MediaCheck.Ok);
        record?.FileId.Should().Be(moved.Id.ToString());
        opened?.Location.Should().Be(TestFiles.Link(moved.Id, null));
    }

    [Fact]
    public async Task ReleaseAssetStore_ShouldReadFromTheDatabaseUntilMigrated_WhenArtifactWasPreparedBeforeFiles()
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
        await Scope(after).GetRequiredService<FilesMediaBackfillService>().RunAsync(default);
        var designs = Scope(after).GetRequiredService<IDesignRepository>();
        var link = await Scope(after).GetRequiredService<IReleaseAssetStore>().LinkAsync(sha, default);
        var file = await designs.ReleaseFileAsync(sha, default);

        stored.Rows.Should().ContainSingle();
        stored.Files.Should().BeEmpty();
        before.Should().BeNull();
        file?.FileId.Should().Be(moved.Id.ToString());
        file?.ContentType.Should().Be("image/png");
        link.Should().Be(TestFiles.Link(moved.Id, null, FileDisposition.Attachment));
        (await designs.AssetAsync(sha, default)).Should().NotBeNull();
    }

    [Fact]
    public async Task MediaStore_ShouldFailCheckAndRestoreOnReupload_WhenFileWasDeletedInFiles()
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

        deleted.Should().Be(MediaCheck.Missing);
        restored.FileId.Should().Be(stored.FileId);
        afterRestore.Should().Be(MediaCheck.Ok);
        files.Uploads.Should().Be(uploadsBefore + 1);
        replaced.FileId.Should().NotBe(stored.FileId);
        record?.FileId.Should().Be(replaced.FileId);
        afterReplace.Should().Be(MediaCheck.Ok);
    }

    private Task<ServiceProvider> ServicesAsync(TestFiles? files)
    {
        Directory.CreateDirectory(root);
        var settings = new Dictionary<string, string?>
        {
            [CatalogueTestDatabase.ConnectionKey] = CatalogueTestDatabase.For(Path.Combine(root, "catalogue.db")), [CatalogueTestDatabase.OwnerKey] = CatalogueTestDatabase.OwnerInstance,
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
        return Task.FromResult(services);
    }

    private static IServiceProvider Scope(IServiceProvider services) => services.CreateScope().ServiceProvider;

    private static void Use(IServiceCollection services, TestFiles files) => files.Register(services);

    private static string Sha(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
