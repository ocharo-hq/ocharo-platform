using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql;

namespace OcharoBrand.Tests.Support;

/// <summary>Writes a retired Brand SQLite store as its last release left it: the tables EF Core's <c>EnsureCreated</c> made,
/// Guids as upper-case text and every instant through EF Core's binary converter.</summary>
public static class LegacyBrandStore
{
    private static readonly DateTimeOffsetToBinaryConverter BinaryInstant = new();

    /// <summary>Each table's columns, SQLite name and PostgreSQL name, key first.</summary>
    private static readonly (string Table, (string Sqlite, string Postgres)[] Columns)[] Tables =
    [
        ("drafts", [("Id", "id"), ("Revision", "revision"), ("UpdatedAt", "updated_at"), ("Json", "json")]),
        ("draft_revisions", [("Id", "id"), ("Revision", "revision"), ("SavedAt", "saved_at"), ("Summary", "summary"), ("Json", "json")]),
        ("logo_versions", [("Id", "id"), ("Slot", "slot"), ("Sha256", "sha256"), ("UploadedAt", "uploaded_at"), ("Json", "json")]),
        ("releases", [("Version", "version"), ("PublishedAt", "published_at"), ("Json", "json")]),
        ("appearances", [("Id", "id"), ("StartsAt", "starts_at"), ("EndsAt", "ends_at"), ("Status", "status"), ("Json", "json")]),
        ("media_blobs",
        [
            ("Sha256", "sha256"), ("ContentType", "content_type"), ("ByteLength", "byte_length"), ("FileId", "file_id"), ("BlobPath", "blob_path"),
            ("PreviewPath", "preview_path"), ("Width", "width"), ("Height", "height"), ("StoredAt", "stored_at"),
        ]),
        ("consumers", [("App", "app"), ("LastSeenAt", "last_seen_at"), ("LastRelease", "last_release"), ("LastPath", "last_path")]),
    ];

    /// <summary>Creates the store's tables in WAL mode; <paramref name="withFileId"/> false leaves out the media file id, as a
    /// store made before WoW2 Files had it.</summary>
    public static void Create(string path, bool withFileId = true)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var connection = Open(path);
        using var create = connection.CreateCommand();
        create.CommandText =
            $"""
            PRAGMA journal_mode = WAL;
            CREATE TABLE "appearances" ("Id" TEXT NOT NULL CONSTRAINT "PK_appearances" PRIMARY KEY, "StartsAt" INTEGER NOT NULL,
                "EndsAt" INTEGER NOT NULL, "Status" TEXT NOT NULL, "Json" TEXT NOT NULL);
            CREATE TABLE "consumers" ("App" TEXT NOT NULL CONSTRAINT "PK_consumers" PRIMARY KEY, "LastSeenAt" INTEGER NOT NULL,
                "LastRelease" INTEGER NOT NULL, "LastPath" TEXT NOT NULL);
            CREATE TABLE "draft_revisions" ("Id" TEXT NOT NULL CONSTRAINT "PK_draft_revisions" PRIMARY KEY, "Revision" INTEGER NOT NULL,
                "SavedAt" INTEGER NOT NULL, "Summary" TEXT NOT NULL, "Json" TEXT NOT NULL);
            CREATE TABLE "drafts" ("Id" TEXT NOT NULL CONSTRAINT "PK_drafts" PRIMARY KEY, "Revision" INTEGER NOT NULL,
                "UpdatedAt" INTEGER NOT NULL, "Json" TEXT NOT NULL);
            CREATE TABLE "logo_versions" ("Id" TEXT NOT NULL CONSTRAINT "PK_logo_versions" PRIMARY KEY, "Slot" TEXT NOT NULL,
                "Sha256" TEXT NOT NULL, "UploadedAt" INTEGER NOT NULL, "Json" TEXT NOT NULL);
            CREATE TABLE "media_blobs" ("Sha256" TEXT NOT NULL CONSTRAINT "PK_media_blobs" PRIMARY KEY, "ContentType" TEXT NOT NULL,
                "ByteLength" INTEGER NOT NULL, "BlobPath" TEXT NOT NULL, "PreviewPath" TEXT NULL, "Width" INTEGER NULL,
                "Height" INTEGER NULL, "StoredAt" INTEGER NOT NULL{(withFileId ? ", \"FileId\" TEXT NULL" : "")});
            CREATE TABLE "releases" ("Version" INTEGER NOT NULL CONSTRAINT "PK_releases" PRIMARY KEY, "PublishedAt" INTEGER NOT NULL,
                "Json" TEXT NOT NULL);
            CREATE INDEX "IX_appearances_Status_StartsAt" ON "appearances" ("Status", "StartsAt");
            CREATE UNIQUE INDEX "IX_draft_revisions_Revision" ON "draft_revisions" ("Revision");
            CREATE UNIQUE INDEX "IX_logo_versions_Slot_Sha256" ON "logo_versions" ("Slot", "Sha256");
            """;
        create.ExecuteNonQuery();
    }

    /// <summary>Inserts one row, by SQLite column name, encoding Guids and instants as the store did.</summary>
    public static void Insert(string path, string table, IReadOnlyDictionary<string, object?> values)
    {
        using var connection = Open(path);
        using var insert = connection.CreateCommand();
        var names = values.Keys.ToList();
        insert.CommandText = $"INSERT INTO \"{table}\" ({string.Join(", ", names.Select(name => $"\"{name}\""))}) " +
            $"VALUES ({string.Join(", ", names.Select((_, index) => $"$p{index}"))})";
        for (var index = 0; index < names.Count; index++)
            insert.Parameters.AddWithValue($"$p{index}", Encode(values[names[index]]));
        insert.ExecuteNonQuery();
    }

    /// <summary>Writes every row of a product database's <c>brand</c> schema into a new store at <paramref name="path"/>, as
    /// the SQLite release would have held them.</summary>
    public static async Task CopyFromPostgresAsync(string connectionString, string path)
    {
        Create(path);
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        foreach (var (table, columns) in Tables)
        {
            await using var command = dataSource.CreateCommand($"SELECT {string.Join(", ", columns.Select(column => column.Postgres))} FROM brand.{table}");
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object?>(StringComparer.Ordinal);
                for (var index = 0; index < columns.Length; index++)
                {
                    row[columns[index].Sqlite] = reader.IsDBNull(index) ? null
                        : reader.GetDataTypeName(index) == "timestamp with time zone" ? reader.GetFieldValue<DateTimeOffset>(index)
                        : reader.GetValue(index);
                }

                Insert(path, table, row);
            }
        }

        SqliteConnection.ClearAllPools();
    }

    private static object Encode(object? value) => value switch
    {
        null => DBNull.Value,
        Guid id => id.ToString("D").ToUpperInvariant(),
        DateTimeOffset instant => BinaryInstant.ConvertToProvider(instant)!,
        _ => value,
    };

    private static SqliteConnection Open(string path)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ConnectionString);
        connection.Open();
        return connection;
    }
}
