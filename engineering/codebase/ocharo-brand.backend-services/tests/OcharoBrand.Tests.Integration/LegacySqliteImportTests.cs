using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using OcharoBrand.Persistence;
using OcharoBrand.Persistence.Import;
using OcharoBrand.Tests.Support;

namespace OcharoBrand.Tests.Integration;

/// <summary>The one-time copy of the retired SQLite store into schema <c>brand</c>: every row lands with its id, revision,
/// SHA-256, file id and instant, each table's digests agree, a rerun changes nothing, and a diverged row, a corrupt blob or
/// a dry run keeps the SQLite file in place.</summary>
public sealed class LegacySqliteImportTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Published = new(2026, 9, 12, 8, 30, 15, 123, TimeSpan.Zero);
    private static readonly Guid Revision = Guid.Parse("6f9b2c1d-4e5a-4b7c-8d9e-0a1b2c3d4e5f");

    private readonly string root = Directory.CreateTempSubdirectory("ocharo-brand-import-").FullName;
    private readonly byte[] logo = TestArtwork.Png(10);
    private NpgsqlDataSource dataSource = null!;

    private string DatabasePath => Path.Combine(root, "ocharo-brand.db");

    private string KeysPath => Path.Combine(root, "keys");

    private string BlobsPath => Path.Combine(root, "blobs");

    private string LogoSha => Convert.ToHexStringLower(SHA256.HashData(logo));

    public async Task InitializeAsync() => dataSource = NpgsqlDataSource.Create(await BrandTestDatabase.CreateAsync());

    public async Task DisposeAsync()
    {
        await dataSource.DisposeAsync();
        SqliteConnection.ClearAllPools();
        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public async Task RunAsync_ShouldCopyVerifyAndRetire_ThenFindNothingToCopy()
    {
        WriteLegacy(withFileId: true);
        WriteKey("key-1", "<key id=\"1\"/>");

        var report = await Import().RunAsync(dryRun: false, CancellationToken.None);

        report.Status.Should().Be(LegacySqliteImportStatus.Imported, report.Summary);
        report.Rows.Should().Be(7);
        report.Copied.Should().Be(7);
        report.Diverged.Should().Be(0);
        report.Releases.Should().Be(1);
        report.FileIds.Should().Be(1);
        report.BlobsVerified.Should().Be(1);
        report.KeysCopied.Should().Be(1);
        report.Tables.Should().AllSatisfy(table => table.PostgresSha256.Should().Be(table.Sha256, table.Name));
        File.Exists(DatabasePath).Should().BeFalse();
        File.Exists(Path.Combine(root, report.RetiredTo!)).Should().BeTrue();
        (await ScalarAsync<int>("SELECT revision FROM brand.drafts WHERE id = 'brand'")).Should().Be(7);
        (await ScalarAsync<int>("SELECT revision FROM brand.draft_revisions WHERE id = $1", Revision)).Should().Be(7);
        (await ScalarAsync<DateTimeOffset>("SELECT published_at FROM brand.releases WHERE version = 1")).Should().Be(Published);
        (await ScalarAsync<string>("SELECT json FROM brand.releases WHERE version = 1")).Should().Be("""{"version":1, "note":"first kit"}""");
        (await ScalarAsync<string>("SELECT file_id FROM brand.media_blobs WHERE sha256 = $1", LogoSha)).Should().Be("file-1");
        new PostgresXmlRepository(dataSource).GetAllElements().Should().ContainSingle();

        var rerun = await Import().RunAsync(dryRun: false, CancellationToken.None);

        rerun.Status.Should().Be(LegacySqliteImportStatus.Current);
    }

    [Fact]
    public async Task RunAsync_ShouldCopyEmptyFileIds_WhenTheStorePredatesFiles()
    {
        WriteLegacy(withFileId: false);

        var report = await Import().RunAsync(dryRun: false, CancellationToken.None);

        report.Status.Should().Be(LegacySqliteImportStatus.Imported, report.Summary);
        report.FileIds.Should().Be(0);
        (await ScalarAsync<object>("SELECT file_id FROM brand.media_blobs WHERE sha256 = $1", LogoSha)).Should().Be(DBNull.Value);
    }

    [Fact]
    public async Task RunAsync_ShouldRetireAfterARerun_WhenTheRowsAreAlreadyIdentical()
    {
        WriteLegacy(withFileId: true);
        var first = await Import().RunAsync(dryRun: false, CancellationToken.None);
        File.Move(Path.Combine(root, first.RetiredTo!), DatabasePath);

        var report = await Import().RunAsync(dryRun: false, CancellationToken.None);

        report.Status.Should().Be(LegacySqliteImportStatus.Imported, report.Summary);
        report.Copied.Should().Be(0);
        report.Diverged.Should().Be(0);
        File.Exists(DatabasePath).Should().BeFalse();
    }

    [Fact]
    public async Task RunAsync_ShouldRefuseAndKeepTheFile_WhenPostgreSqlHoldsAnotherDraft()
    {
        WriteLegacy(withFileId: true);
        await ExecuteAsync("INSERT INTO brand.drafts (id, revision, updated_at, json) VALUES ('brand', 1, now(), '{}')");

        var report = await Import().RunAsync(dryRun: false, CancellationToken.None);

        report.Status.Should().Be(LegacySqliteImportStatus.Refused);
        report.Diverged.Should().Be(1);
        report.Tables.Single(table => table.Name == "drafts").PostgresSha256.Should().NotBe(report.Tables.Single(table => table.Name == "drafts").Sha256);
        File.Exists(DatabasePath).Should().BeTrue();
    }

    [Fact]
    public async Task RunAsync_ShouldRefuseAndKeepTheFile_WhenALocalBlobDisagreesWithItsSha256()
    {
        WriteLegacy(withFileId: true);
        File.WriteAllBytes(Path.Combine(BlobsPath, "media", LogoSha[..2], LogoSha), TestArtwork.Png(11));

        var report = await Import().RunAsync(dryRun: false, CancellationToken.None);

        report.Status.Should().Be(LegacySqliteImportStatus.Refused);
        report.BlobsDiverged.Should().Be(1);
        File.Exists(DatabasePath).Should().BeTrue();
    }

    [Fact]
    public async Task RunAsync_ShouldWriteNothing_OnADryRun()
    {
        WriteLegacy(withFileId: true);
        WriteKey("key-1", "<key id=\"1\"/>");

        var report = await Import().RunAsync(dryRun: true, CancellationToken.None);

        report.Status.Should().Be(LegacySqliteImportStatus.Planned);
        report.Copied.Should().Be(7);
        report.Tables.Should().AllSatisfy(table => table.PostgresSha256.Should().BeNull());
        (await ScalarAsync<long>("SELECT (SELECT count(*) FROM brand.drafts) + (SELECT count(*) FROM brand.releases) + (SELECT count(*) FROM brand.data_protection_keys)")).Should().Be(0);
        File.Exists(DatabasePath).Should().BeTrue();
    }

    [Fact]
    public async Task RunAsync_ShouldFindNothingToCopy_WithoutASqliteStore()
    {
        var report = await Import().RunAsync(dryRun: false, CancellationToken.None);

        report.Status.Should().Be(LegacySqliteImportStatus.Current);
    }

    private LegacySqliteImport Import() =>
        new(dataSource, new LegacySqliteSettings { Path = DatabasePath, KeysPath = KeysPath, BlobsPath = BlobsPath }, TimeProvider.System,
            NullLogger<LegacySqliteImport>.Instance);

    /// <summary>Writes a store holding one row in every table and the logo's blob: release 1, revision 7 and its snapshot,
    /// a logo version, a scheduled appearance, the logo's media row and one consumer.</summary>
    private void WriteLegacy(bool withFileId)
    {
        LegacyBrandStore.Create(DatabasePath, withFileId);
        var at = Published.AddDays(-1).ToOffset(TimeSpan.FromHours(5));
        LegacyBrandStore.Insert(DatabasePath, "drafts", new Dictionary<string, object?> { ["Id"] = "brand", ["Revision"] = 7, ["UpdatedAt"] = at, ["Json"] = """{"identity":{"name":"Ocharo"}}""" });
        LegacyBrandStore.Insert(DatabasePath, "draft_revisions", new Dictionary<string, object?> { ["Id"] = Revision, ["Revision"] = 7, ["SavedAt"] = at, ["Summary"] = "Saved the identity", ["Json"] = """{"identity":{"name":"Ocharo"}}""" });
        LegacyBrandStore.Insert(DatabasePath, "logo_versions", new Dictionary<string, object?> { ["Id"] = "logo-1", ["Slot"] = "master", ["Sha256"] = LogoSha, ["UploadedAt"] = at, ["Json"] = "{}" });
        LegacyBrandStore.Insert(DatabasePath, "releases", new Dictionary<string, object?> { ["Version"] = 1, ["PublishedAt"] = Published, ["Json"] = """{"version":1, "note":"first kit"}""" });
        LegacyBrandStore.Insert(DatabasePath, "appearances", new Dictionary<string, object?> { ["Id"] = "navruz", ["StartsAt"] = Published.AddDays(10), ["EndsAt"] = Published.AddDays(12), ["Status"] = "scheduled", ["Json"] = "{}" });
        var media = new Dictionary<string, object?>
        {
            ["Sha256"] = LogoSha, ["ContentType"] = "image/png", ["ByteLength"] = (long)logo.Length, ["BlobPath"] = $"media/{LogoSha[..2]}/{LogoSha}",
            ["PreviewPath"] = null, ["Width"] = 64, ["Height"] = 64, ["StoredAt"] = at,
        };
        if (withFileId) media["FileId"] = "file-1";
        LegacyBrandStore.Insert(DatabasePath, "media_blobs", media);
        LegacyBrandStore.Insert(DatabasePath, "consumers", new Dictionary<string, object?> { ["App"] = "catalogue", ["LastSeenAt"] = at, ["LastRelease"] = 1, ["LastPath"] = "/api/public/brand" });
        Directory.CreateDirectory(Path.Combine(BlobsPath, "media", LogoSha[..2]));
        File.WriteAllBytes(Path.Combine(BlobsPath, "media", LogoSha[..2], LogoSha), logo);
    }

    private void WriteKey(string name, string xml)
    {
        Directory.CreateDirectory(KeysPath);
        File.WriteAllText(Path.Combine(KeysPath, name + ".xml"), xml);
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var command = dataSource.CreateCommand(sql);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<T> ScalarAsync<T>(string sql, params object[] parameters)
    {
        await using var command = dataSource.CreateCommand(sql);
        foreach (var parameter in parameters)
            command.Parameters.AddWithValue(parameter);
        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        return reader.IsDBNull(0) && typeof(T) == typeof(object) ? (T)(object)DBNull.Value : reader.GetFieldValue<T>(0);
    }
}
