using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Npgsql;
using OcharoCatalogue.Application.Catalogue.Core.Repositories;
using OcharoCatalogue.Application.Catalogue.Core.Serializers;
using OcharoCatalogue.Domain.Catalogue.Models;
using OcharoCatalogue.Infrastructure.Catalogue.Products.Mappers;
using OcharoCatalogue.Persistence;
using OcharoCatalogue.Persistence.Import;

namespace OcharoCatalogue.Tests.Integration;

/// <summary>The one-time copy of the retired SQLite store: every row, hash, Files id and key lands as it was and the
/// published edition still reads; a dry run writes nothing; a row PostgreSQL holds otherwise refuses the whole copy and
/// keeps the file; a rerun copies nothing.</summary>
public sealed class LegacySqliteImportTests : IAsyncLifetime
{
    private readonly string root = Directory.CreateTempSubdirectory("ocharo-catalogue-import-").FullName;
    private readonly List<CatalogueHarness> harnesses = [];

    private string StorePath => Path.Combine(root, "ocharo-catalogue.db");

    private string KeysPath => Path.Combine(root, "keys");

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        foreach (var harness in harnesses)
            await harness.DisposeAsync();
        SqliteConnection.ClearAllPools();
        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public async Task Import_ShouldCopyVerifyAndRetire_ThenFindNothingToCopy_WhenTheStoreHoldsAPublishedEditionOnFiles()
    {
        var files = new TestFiles();
        var source = await Harness(files);
        var (_, releaseId, _) = await PublishedEditionFixture.PublishAsync(source);
        await LegacyCatalogueStore.WriteAsync(Database(source), StorePath);
        LegacyCatalogueStore.WriteKey(KeysPath, "key-7d1c", "<key id=\"7d1c\"/>");
        var target = await Harness(files, Legacy());

        var report = await target.Get<LegacySqliteImport>().RunAsync(dryRun: false, CancellationToken.None);

        report.Status.Should().Be(LegacySqliteImportStatus.Imported, report.Summary);
        report.Tables.Should().AllSatisfy(table =>
        {
            table.Diverged.Should().Be(0);
            table.Copied.Should().Be(table.Rows);
            table.PostgresSha256.Should().Be(table.SqliteSha256, table.Name);
        });
        report.Tables.Single(table => table.Name == "product_revisions").Rows.Should().Be(2);
        report.Tables.Single(table => table.Name == "catalogue_release_files").Rows.Should().BeGreaterThan(0);
        report.Keys!.Copied.Should().Be(1);
        report.FileIds.Should().BeGreaterThan(1);
        report.Publications.Should().Be(1);
        File.Exists(StorePath).Should().BeFalse();
        File.Exists(Path.Combine(root, report.RetiredTo!)).Should().BeTrue();

        var edition = Json(await target.Releases.PublicAsync(PublishedEditionFixture.Slug, PublishedEditionFixture.Locale, null, CancellationToken.None));
        edition.GetProperty("id").GetGuid().Should().Be(releaseId);
        var pdf = await target.Releases.PublicAssetAsync(PublishedEditionFixture.Slug, PublishedEditionFixture.Locale, releaseId, "catalogue.pdf", CancellationToken.None);
        var sourcePdf = await source.Releases.PublicAssetAsync(PublishedEditionFixture.Slug, PublishedEditionFixture.Locale, releaseId, "catalogue.pdf", CancellationToken.None);
        pdf.Location.Should().NotBeNull().And.Be(sourcePdf.Location);
        var media = (await target.Get<ICatalogueRepository>().GetProductsAsync(CancellationToken.None)).Single();
        (await target.Get<ICatalogueRepository>().GetRevisionsAsync(media.Id, CancellationToken.None)).Should().HaveCount(2);
        new PostgresXmlRepository(target.Get<NpgsqlDataSource>()).GetAllElements().Should().ContainSingle();

        var again = await target.Get<LegacySqliteImport>().RunAsync(dryRun: false, CancellationToken.None);
        again.Status.Should().Be(LegacySqliteImportStatus.Current);
    }

    [Fact]
    public async Task Import_ShouldCopyBytesAndTolerateMissingColumns_WhenTheStorePredatesFiles()
    {
        var source = await Harness();
        var (_, releaseId, sourcePdf) = await PublishedEditionFixture.PublishAsync(source);
        await LegacyCatalogueStore.WriteAsync(Database(source), StorePath, oldShape: true);
        var target = await Harness(settings: Legacy());

        var report = await target.Get<LegacySqliteImport>().RunAsync(dryRun: false, CancellationToken.None);

        report.Status.Should().Be(LegacySqliteImportStatus.Imported, report.Summary);
        report.FileIds.Should().Be(0);
        report.Tables.Single(table => table.Name == "catalogue_release_assets").Rows.Should().BeGreaterThan(0);
        report.Tables.Should().AllSatisfy(table => table.PostgresSha256.Should().Be(table.SqliteSha256, table.Name));
        var pdf = await target.Releases.PublicAssetAsync(PublishedEditionFixture.Slug, PublishedEditionFixture.Locale, releaseId, "catalogue.pdf", CancellationToken.None);
        pdf.Bytes.Should().Equal(sourcePdf);
    }

    [Fact]
    public async Task Import_ShouldWriteNothing_OnADryRun()
    {
        var source = await Harness();
        await PublishedEditionFixture.PublishAsync(source);
        await LegacyCatalogueStore.WriteAsync(Database(source), StorePath);
        var target = await Harness(settings: Legacy());

        var report = await target.Get<LegacySqliteImport>().RunAsync(dryRun: true, CancellationToken.None);

        report.Status.Should().Be(LegacySqliteImportStatus.Planned, report.Summary);
        report.Tables.Should().AllSatisfy(table => table.Copied.Should().Be(table.Rows));
        (await CountAsync(target, "products")).Should().Be(0);
        (await CountAsync(target, "catalogue_releases")).Should().Be(0);
        File.Exists(StorePath).Should().BeTrue();
    }

    [Fact]
    public async Task Import_ShouldRefuseAndWriteNothing_WhenPostgreSqlHoldsARowWithOtherContent()
    {
        var source = await Harness();
        await PublishedEditionFixture.PublishAsync(source);
        await LegacyCatalogueStore.WriteAsync(Database(source), StorePath);
        var target = await Harness(settings: Legacy());
        var copy = new ProductLocale("draft", "Another", null, null, null);
        var other = new ProductDocument { Id = PublishedEditionFixture.ProductId, GarmentType = "nimcha", VariantCode = "v01", Status = "draft", Locales = new(copy, copy, copy) };
        await target.Get<ICatalogueRepository>().InsertProductAsync(ProductProjections.ToRecord(other, 1, target.Clock.GetUtcNow(), target.Clock.GetUtcNow()), "Written before the copy", CancellationToken.None);

        var report = await target.Get<LegacySqliteImport>().RunAsync(dryRun: false, CancellationToken.None);

        report.Status.Should().Be(LegacySqliteImportStatus.Refused);
        report.Tables.Single(table => table.Name == "products").Diverged.Should().Be(1);
        (await CountAsync(target, "products")).Should().Be(1);
        (await CountAsync(target, "catalogue_designs")).Should().Be(0);
        File.Exists(StorePath).Should().BeTrue();
    }

    [Fact]
    public async Task Import_ShouldOnlyRetire_WhenARerunFindsEveryRowAlreadyCopied()
    {
        var source = await Harness();
        await PublishedEditionFixture.PublishAsync(source);
        await LegacyCatalogueStore.WriteAsync(Database(source), StorePath);
        var target = await Harness(settings: Legacy());
        var first = await target.Get<LegacySqliteImport>().RunAsync(dryRun: false, CancellationToken.None);
        // As if the retire had failed after the commit: the file is back where the import looks.
        File.Move(Path.Combine(root, first.RetiredTo!), StorePath);

        var rerun = await target.Get<LegacySqliteImport>().RunAsync(dryRun: false, CancellationToken.None);

        rerun.Status.Should().Be(LegacySqliteImportStatus.Imported, rerun.Summary);
        rerun.Tables.Should().AllSatisfy(table => table.Copied.Should().Be(0));
        File.Exists(StorePath).Should().BeFalse();
    }

    [Fact]
    public async Task Import_ShouldFindNothingToCopy_WithoutASqliteStore()
    {
        var target = await Harness(settings: Legacy());

        var report = await target.Get<LegacySqliteImport>().RunAsync(dryRun: false, CancellationToken.None);

        report.Status.Should().Be(LegacySqliteImportStatus.Current);
    }

    private async Task<CatalogueHarness> Harness(TestFiles? files = null, IReadOnlyDictionary<string, string?>? settings = null)
    {
        var harness = await CatalogueHarness.CreateAsync(files: files, settings: settings);
        harnesses.Add(harness);
        return harness;
    }

    private Dictionary<string, string?> Legacy() => new()
    {
        ["LegacySqlite:Path"] = StorePath,
        ["LegacySqlite:KeysPath"] = KeysPath,
    };

    private static string Database(CatalogueHarness harness) => harness.Get<IConfiguration>()[CatalogueTestDatabase.ConnectionKey]!;

    private static async Task<long> CountAsync(CatalogueHarness harness, string table)
    {
        await using var command = harness.Get<NpgsqlDataSource>().CreateCommand($"SELECT count(*) FROM {CatalogueDatabaseConstants.Schema}.{table}");
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private static JsonElement Json(object value) => JsonSerializer.SerializeToElement(value, CatalogueJson.Options);
}
