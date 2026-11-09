using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql;
using OcharoCatalogue.Persistence;
using OcharoCatalogue.Persistence.Import;

namespace OcharoCatalogue.Tests.Harness;

/// <summary>Writes a retired SQLite store as the SQLite releases left it — EF's tables plus the designer tables the boot
/// added — from the rows a PostgreSQL catalogue holds, so an import test starts from a real catalogue's data.</summary>
/// <remarks>Values take the SQLite release's storage: GUIDs as upper-case text, instants as EF's binary long, flags as
/// 0/1. The old shape is a store made before WoW2 Files and withdrawal: no <c>media_blobs.FileId</c>, no
/// <c>catalogue_releases.WithdrawnAtUtc</c> and no <c>catalogue_release_files</c>.</remarks>
public static class LegacyCatalogueStore
{
    private static readonly DateTimeOffsetToBinaryConverter BinaryInstant = new();

    /// <summary>Copies every row of <paramref name="source"/>'s <c>catalogue</c> schema into a new SQLite file at
    /// <paramref name="path"/>.</summary>
    public static async Task WriteAsync(string source, string path, bool oldShape = false)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var sqlite = new SqliteConnection($"Data Source={path};Pooling=False");
        await sqlite.OpenAsync();
        await ExecuteAsync(sqlite, Schema(oldShape));
        await using var postgres = new NpgsqlConnection(source);
        await postgres.OpenAsync();
        foreach (var table in LegacySqliteTables.All)
        {
            if (oldShape && table.Name == "catalogue_release_files") continue;
            var columns = table.Columns
                .Where(column => !oldShape || column.Sqlite is not ("FileId" or "WithdrawnAtUtc") || table.Name == "catalogue_release_files")
                .ToList();
            await using var select = new NpgsqlCommand(
                $"SELECT {string.Join(", ", columns.Select(column => column.Postgres))} FROM {CatalogueDatabaseConstants.Schema}.{table.Name}",
                postgres);
            await using var reader = await select.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                await using var insert = sqlite.CreateCommand();
                insert.CommandText = $"INSERT INTO \"{table.Name}\" ({string.Join(", ", columns.Select(column => $"\"{column.Sqlite}\""))}) "
                    + $"VALUES ({string.Join(", ", columns.Select((_, index) => $"$p{index}"))})";
                for (var index = 0; index < columns.Count; index++)
                    insert.Parameters.AddWithValue($"$p{index}", Stored(reader, index, columns[index].Kind));
                await insert.ExecuteNonQueryAsync();
            }
        }
    }

    /// <summary>Writes one key of the file key ring the SQLite release kept beside its store.</summary>
    public static void WriteKey(string folder, string friendlyName, string xml)
    {
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, friendlyName + ".xml"), xml);
    }

    /// <summary>Runs one SQL statement against the SQLite file, as a stray edit made before the move would.</summary>
    public static async Task ExecuteAsync(string path, string sql)
    {
        await using var sqlite = new SqliteConnection($"Data Source={path};Pooling=False");
        await sqlite.OpenAsync();
        await ExecuteAsync(sqlite, sql);
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static object Stored(NpgsqlDataReader reader, int index, LegacyColumnKind kind)
    {
        if (reader.IsDBNull(index)) return DBNull.Value;
        return kind switch
        {
            LegacyColumnKind.Uuid => reader.GetGuid(index).ToString("D").ToUpperInvariant(),
            LegacyColumnKind.Instant => BinaryInstant.ConvertToProvider(reader.GetFieldValue<DateTimeOffset>(index))!,
            LegacyColumnKind.Boolean => reader.GetBoolean(index) ? 1L : 0L,
            LegacyColumnKind.Integer => (long)reader.GetInt32(index),
            _ => reader.GetValue(index),
        };
    }

    /// <summary>The SQLite release's schema: EF's <c>EnsureCreated</c> tables, then the designer tables and columns the boot
    /// added.</summary>
    private static string Schema(bool oldShape) =>
        $"""
        PRAGMA journal_mode = WAL;
        CREATE TABLE "collections" ("Id" TEXT NOT NULL CONSTRAINT "PK_collections" PRIMARY KEY, "SortOrder" INTEGER NOT NULL, "Revision" INTEGER NOT NULL, "UpdatedAt" INTEGER NOT NULL, "Json" TEXT NOT NULL);
        CREATE TABLE "media_blobs" ("Sha256" TEXT NOT NULL CONSTRAINT "PK_media_blobs" PRIMARY KEY, "ContentType" TEXT NOT NULL, "ByteLength" INTEGER NOT NULL, "BlobPath" TEXT NOT NULL, "PreviewPath" TEXT NULL, "Width" INTEGER NULL, "Height" INTEGER NULL, "StoredAt" INTEGER NOT NULL);
        CREATE TABLE "product_revisions" ("Id" TEXT NOT NULL CONSTRAINT "PK_product_revisions" PRIMARY KEY, "ProductId" TEXT NOT NULL, "Revision" INTEGER NOT NULL, "SavedAt" INTEGER NOT NULL, "Summary" TEXT NOT NULL, "Json" TEXT NOT NULL);
        CREATE TABLE "products" ("Id" TEXT NOT NULL CONSTRAINT "PK_products" PRIMARY KEY, "GarmentType" TEXT NOT NULL, "Status" TEXT NOT NULL, "SortOrder" INTEGER NOT NULL, "Revision" INTEGER NOT NULL, "CreatedAt" INTEGER NOT NULL, "UpdatedAt" INTEGER NOT NULL, "Json" TEXT NOT NULL);
        CREATE TABLE "settings" ("Id" TEXT NOT NULL CONSTRAINT "PK_settings" PRIMARY KEY, "Revision" INTEGER NOT NULL, "Json" TEXT NOT NULL);
        CREATE UNIQUE INDEX "IX_product_revisions_ProductId_Revision" ON "product_revisions" ("ProductId", "Revision");
        CREATE TABLE catalogue_designs (Id TEXT NOT NULL PRIMARY KEY, Title TEXT NOT NULL, Slug TEXT NOT NULL, Locale TEXT NOT NULL, Revision INTEGER NOT NULL, Archived INTEGER NOT NULL, CreatedAtUtc INTEGER NOT NULL, UpdatedAtUtc INTEGER NOT NULL, Json TEXT NOT NULL);
        CREATE TABLE catalogue_design_revisions (Id TEXT NOT NULL PRIMARY KEY, DesignId TEXT NOT NULL, Revision INTEGER NOT NULL, Title TEXT NOT NULL, SavedAtUtc INTEGER NOT NULL, Json TEXT NOT NULL);
        CREATE TABLE catalogue_releases (Id TEXT NOT NULL PRIMARY KEY, DesignId TEXT NOT NULL, DesignRevision INTEGER NOT NULL, Slug TEXT NOT NULL, Locale TEXT NOT NULL, CreatedAtUtc INTEGER NOT NULL, PublishedAtUtc INTEGER NULL,{(oldShape ? "" : " WithdrawnAtUtc INTEGER NULL,")} Fingerprint TEXT NOT NULL, ProductIdsJson TEXT NOT NULL, Json TEXT NOT NULL);
        CREATE TABLE catalogue_publications (Key TEXT NOT NULL PRIMARY KEY, DesignId TEXT NOT NULL, ReleaseId TEXT NOT NULL);
        CREATE TABLE catalogue_release_assets (Sha256 TEXT NOT NULL PRIMARY KEY, ContentType TEXT NOT NULL, Bytes BLOB NOT NULL);
        {(oldShape ? "" : "CREATE TABLE catalogue_release_files (Sha256 TEXT NOT NULL PRIMARY KEY, ContentType TEXT NOT NULL, FileId TEXT NOT NULL);")}
        {(oldShape ? "" : "ALTER TABLE media_blobs ADD COLUMN FileId TEXT NULL;")}
        """;
}
