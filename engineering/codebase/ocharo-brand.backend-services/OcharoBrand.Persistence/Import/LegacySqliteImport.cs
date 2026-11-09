using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;

namespace OcharoBrand.Persistence.Import;

/// <summary>Copies the retired SQLite store and its file key ring into schema <c>brand</c> once, verifies the copy table by
/// table, row by row, then retires the SQLite file so the copy never runs again.</summary>
/// <remarks>
///   - every row keeps its id, revision, SHA-256, Files file id, document text and instant; an instant is stored in UTC,
///     the store's 0.1 ms ticks intact
///   - each table is verified by its row count and a SHA-256 over each row's canonical content, read back from PostgreSQL
///   - idempotent: a row or key already in PostgreSQL is kept; a rerun after a failed retire finds every row identical and
///     only retires the file
///   - a row whose PostgreSQL copy differs, a row only PostgreSQL holds, a key missing after the copy or a local blob whose
///     bytes disagree with its row's SHA-256 refuses the run and keeps the file, so nothing is lost
///   - the local blob folder is read, never moved: files stored before WoW2 Files keep their bytes there
///   - the same code serves dev and prod; the report carries counts and digests only, never documents, ids or keys
/// </remarks>
public sealed partial class LegacySqliteImport(
    NpgsqlDataSource dataSource, LegacySqliteSettings settings, TimeProvider time, ILogger<LegacySqliteImport> logger)
{
    private static readonly DateTimeOffsetToBinaryConverter BinaryInstant = new();

    /// <summary>Every table of the store in copy order, each key first; the media file id is optional, since a store made
    /// before WoW2 Files lacks the column.</summary>
    private static readonly LegacyTable[] Tables =
    [
        new("drafts", [new("Id", "id", Kind.Text), new("Revision", "revision", Kind.Int32), new("UpdatedAt", "updated_at", Kind.Instant), new("Json", "json", Kind.Text)]),
        new("draft_revisions", [new("Id", "id", Kind.Uuid), new("Revision", "revision", Kind.Int32), new("SavedAt", "saved_at", Kind.Instant), new("Summary", "summary", Kind.Text), new("Json", "json", Kind.Text)]),
        new("logo_versions", [new("Id", "id", Kind.Text), new("Slot", "slot", Kind.Text), new("Sha256", "sha256", Kind.Text), new("UploadedAt", "uploaded_at", Kind.Instant), new("Json", "json", Kind.Text)]),
        new("releases", [new("Version", "version", Kind.Int32), new("PublishedAt", "published_at", Kind.Instant), new("Json", "json", Kind.Text)]),
        new("appearances", [new("Id", "id", Kind.Text), new("StartsAt", "starts_at", Kind.Instant), new("EndsAt", "ends_at", Kind.Instant), new("Status", "status", Kind.Text), new("Json", "json", Kind.Text)]),
        new("media_blobs",
        [
            new("Sha256", "sha256", Kind.Text), new("ContentType", "content_type", Kind.Text), new("ByteLength", "byte_length", Kind.Int64),
            new("FileId", "file_id", Kind.Text, Nullable: true, Optional: true), new("BlobPath", "blob_path", Kind.Text),
            new("PreviewPath", "preview_path", Kind.Text, Nullable: true), new("Width", "width", Kind.Int32, Nullable: true),
            new("Height", "height", Kind.Int32, Nullable: true), new("StoredAt", "stored_at", Kind.Instant),
        ]),
        new("consumers", [new("App", "app", Kind.Text), new("LastSeenAt", "last_seen_at", Kind.Instant), new("LastRelease", "last_release", Kind.Int32), new("LastPath", "last_path", Kind.Text)]),
    ];

    /// <summary>Copies, verifies and retires; with <paramref name="dryRun"/> it reads both sides and writes nothing.</summary>
    public async Task<LegacySqliteImportReport> RunAsync(bool dryRun, CancellationToken ct)
    {
        var path = Path.GetFullPath(settings.Path);
        if (!File.Exists(path))
            return new LegacySqliteImportReport { Status = LegacySqliteImportStatus.Current, Summary = "No SQLite store to copy." };

        var legacy = await ReadSqliteAsync(path, ct);
        var keys = ReadKeys(Path.GetFullPath(settings.KeysPath));
        var blobs = await CheckBlobsAsync(legacy["media_blobs"], ct);

        if (dryRun)
        {
            var present = await ReadPostgresAsync(ct);
            var planned = Report(LegacySqliteImportStatus.Planned, legacy, present, copied: null, blobs, keys, keysCopied: 0, keysMissing: 0);
            return planned with { Summary = $"Dry run: nothing written; {planned.Copied} of {planned.Rows} rows and up to {keys.Count} keys to copy, {planned.Diverged} rows already differ." };
        }

        Dictionary<string, int> copied;
        int keysCopied;
        await using (var connection = await dataSource.OpenConnectionAsync(ct))
        await using (var transaction = await connection.BeginTransactionAsync(ct))
        {
            copied = await InsertRowsAsync(connection, transaction, legacy, ct);
            keysCopied = await InsertKeysAsync(connection, transaction, keys, ct);
            await transaction.CommitAsync(ct);
        }

        var stored = await ReadPostgresAsync(ct);
        var keysMissing = await CountMissingKeysAsync(keys, ct);
        var report = Report(LegacySqliteImportStatus.Imported, legacy, stored, copied, blobs, keys, keysCopied, keysMissing);
        if (report.Diverged > 0 || report.KeysMissing > 0 || report.BlobsDiverged > 0)
        {
            LogRefused(logger, report.Diverged, report.KeysMissing, report.BlobsDiverged);
            return report with
            {
                Status = LegacySqliteImportStatus.Refused,
                Summary = $"{report.Diverged} rows differ, {report.KeysMissing} keys are missing and {report.BlobsDiverged} local blobs disagree with their SHA-256; the SQLite store stays in place.",
            };
        }

        var retiredTo = Retire(path);
        LogImported(logger, report.Copied, report.Rows, keysCopied, keys.Count);
        var unreadable = report.BlobsMissing > 0 ? $" {report.BlobsMissing} media rows have neither a Files id nor a local blob, as before." : "";
        return report with
        {
            Summary = $"Copied {report.Copied} of {report.Rows} rows and {keysCopied} of {keys.Count} keys; every row verified.{unreadable}",
            RetiredTo = retiredTo,
        };
    }

    /// <summary>Reads every table in one read transaction, so a writer still running beside the import cannot tear the
    /// snapshot.</summary>
    private static async Task<IReadOnlyDictionary<string, IReadOnlyList<LegacyRow>>> ReadSqliteAsync(string path, CancellationToken ct)
    {
        var builder = new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadWrite, Pooling = false };
        await using var connection = new SqliteConnection(builder.ConnectionString);
        await connection.OpenAsync(ct);
        await using var transaction = connection.BeginTransaction(deferred: true);
        var rows = new Dictionary<string, IReadOnlyList<LegacyRow>>(StringComparer.Ordinal);
        foreach (var table in Tables)
        {
            var columns = await ReadColumnsAsync(connection, transaction, table.Name, ct);
            if (columns.Count == 0)
            {
                rows[table.Name] = [];
                continue;
            }

            var missing = table.Columns.FirstOrDefault(column => !column.Optional && !columns.Contains(column.Sqlite));
            if (missing is not null)
                throw new InvalidOperationException($"The SQLite store's {table.Name} table lacks its {missing.Sqlite} column.");

            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"SELECT {string.Join(", ", table.Columns.Select(column => columns.Contains(column.Sqlite) ? $"\"{column.Sqlite}\"" : "NULL"))} FROM \"{table.Name}\"";
            await using var reader = await command.ExecuteReaderAsync(ct);
            var read = new List<LegacyRow>();
            while (await reader.ReadAsync(ct))
                read.Add(new LegacyRow(table.Columns.Select((column, index) => ReadSqliteValue(reader, index, column)).ToArray()));
            rows[table.Name] = read;
        }

        return rows;
    }

    private static async Task<HashSet<string>> ReadColumnsAsync(SqliteConnection connection, SqliteTransaction transaction, string table, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT name FROM pragma_table_info($table)";
        command.Parameters.AddWithValue("$table", table);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var columns = new HashSet<string>(StringComparer.Ordinal);
        while (await reader.ReadAsync(ct))
            columns.Add(reader.GetString(0));
        return columns;
    }

    private static object? ReadSqliteValue(SqliteDataReader reader, int index, LegacyColumn column)
    {
        if (reader.IsDBNull(index))
            return column.Nullable ? null : throw new InvalidOperationException($"The SQLite store holds a null {column.Sqlite}.");
        return column.Kind switch
        {
            Kind.Text => reader.GetString(index),
            Kind.Int32 => checked((int)reader.GetInt64(index)),
            Kind.Int64 => reader.GetInt64(index),
            // EF Core's SQLite provider stores a Guid as upper-case text.
            Kind.Uuid => reader.GetFieldType(index) == typeof(byte[]) ? new Guid((byte[])reader.GetValue(index)) : Guid.Parse(reader.GetString(index)),
            // Brand's context stored every instant through EF Core's binary converter: 0.1 ms ticks and the offset.
            Kind.Instant => reader.GetFieldType(index) == typeof(string)
                ? DateTimeOffset.Parse(reader.GetString(index), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToUniversalTime()
                : ((DateTimeOffset)BinaryInstant.ConvertFromProvider(reader.GetInt64(index))!).ToUniversalTime(),
            _ => throw new InvalidOperationException($"Unknown column kind {column.Kind}."),
        };
    }

    private static IReadOnlyList<LegacyKey> ReadKeys(string folder) =>
        Directory.Exists(folder)
            ? Directory.GetFiles(folder, "*.xml")
                .Order(StringComparer.Ordinal)
                .Select(file => new LegacyKey(Path.GetFileNameWithoutExtension(file), File.ReadAllText(file)))
                .ToList()
            : [];

    /// <summary>Hashes each media row's local blob, when it has one, against the row's SHA-256.</summary>
    private async Task<BlobCheck> CheckBlobsAsync(IReadOnlyList<LegacyRow> media, CancellationToken ct)
    {
        var root = Path.GetFullPath(settings.BlobsPath);
        var lines = new List<string>();
        int blobs = 0, verified = 0, diverged = 0, missing = 0, fileIds = 0;
        foreach (var row in media.OrderBy(row => (string)row.Values[0]!, StringComparer.Ordinal))
        {
            var sha = (string)row.Values[0]!;
            var fileId = row.Values[3] as string;
            var blobPath = (string)row.Values[4]!;
            if (!string.IsNullOrEmpty(fileId)) fileIds++;
            if (string.IsNullOrEmpty(blobPath))
            {
                lines.Add($"{sha}:none");
                continue;
            }

            blobs++;
            var file = Path.GetFullPath(Path.Combine(root, blobPath));
            if (!file.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal) || !File.Exists(file))
            {
                if (string.IsNullOrEmpty(fileId)) missing++;
                lines.Add($"{sha}:absent");
                continue;
            }

            await using var stream = File.OpenRead(file);
            var actual = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, ct));
            if (actual == sha) verified++;
            else diverged++;
            lines.Add($"{sha}:{actual}");
        }

        return new BlobCheck(blobs, verified, diverged, missing, fileIds, Sha256(string.Join('\n', lines)));
    }

    private static async Task<Dictionary<string, int>> InsertRowsAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, IReadOnlyDictionary<string, IReadOnlyList<LegacyRow>> legacy, CancellationToken ct)
    {
        var copied = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var table in Tables)
        {
            var count = 0;
            var sql = $"INSERT INTO brand.{table.Name} ({string.Join(", ", table.Columns.Select(column => column.Postgres))}) " +
                $"VALUES ({string.Join(", ", table.Columns.Select((_, index) => $"${index + 1}"))}) ON CONFLICT DO NOTHING";
            foreach (var row in legacy[table.Name])
            {
                await using var insert = new NpgsqlCommand(sql, connection, transaction);
                for (var index = 0; index < table.Columns.Length; index++)
                    insert.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = DbType(table.Columns[index].Kind), Value = row.Values[index] ?? DBNull.Value });
                count += await insert.ExecuteNonQueryAsync(ct);
            }

            copied[table.Name] = count;
        }

        return copied;
    }

    private static async Task<int> InsertKeysAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, IReadOnlyList<LegacyKey> keys, CancellationToken ct)
    {
        var copied = 0;
        foreach (var key in keys)
        {
            await using var insert = new NpgsqlCommand(
                "INSERT INTO brand.data_protection_keys (friendly_name, xml) VALUES ($1, $2) ON CONFLICT (friendly_name) DO NOTHING",
                connection, transaction);
            insert.Parameters.AddWithValue(key.FriendlyName);
            insert.Parameters.AddWithValue(key.Xml);
            copied += await insert.ExecuteNonQueryAsync(ct);
        }

        return copied;
    }

    /// <summary>Reads every row PostgreSQL holds in each table, as canonical digests by key.</summary>
    private async Task<IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>> ReadPostgresAsync(CancellationToken ct)
    {
        var tables = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
        foreach (var table in Tables)
        {
            await using var command = dataSource.CreateCommand($"SELECT {string.Join(", ", table.Columns.Select(column => column.Postgres))} FROM brand.{table.Name}");
            await using var reader = await command.ExecuteReaderAsync(ct);
            var rows = new Dictionary<string, string>(StringComparer.Ordinal);
            while (await reader.ReadAsync(ct))
            {
                var row = new LegacyRow(table.Columns.Select((column, index) => ReadPostgresValue(reader, index, column)).ToArray());
                rows[row.Key] = row.Digest;
            }

            tables[table.Name] = rows;
        }

        return tables;
    }

    private static object? ReadPostgresValue(NpgsqlDataReader reader, int index, LegacyColumn column)
    {
        if (reader.IsDBNull(index)) return null;
        return column.Kind switch
        {
            Kind.Text => reader.GetString(index),
            Kind.Int32 => reader.GetInt32(index),
            Kind.Int64 => reader.GetInt64(index),
            Kind.Uuid => reader.GetGuid(index),
            Kind.Instant => reader.GetFieldValue<DateTimeOffset>(index).ToUniversalTime(),
            _ => throw new InvalidOperationException($"Unknown column kind {column.Kind}."),
        };
    }

    private async Task<int> CountMissingKeysAsync(IReadOnlyList<LegacyKey> keys, CancellationToken ct)
    {
        if (keys.Count == 0) return 0;
        await using var command = dataSource.CreateCommand(
            "SELECT friendly_name, xml FROM brand.data_protection_keys WHERE friendly_name = ANY($1)");
        command.Parameters.AddWithValue(keys.Select(key => key.FriendlyName).ToArray());
        await using var reader = await command.ExecuteReaderAsync(ct);
        var stored = new Dictionary<string, string>(StringComparer.Ordinal);
        while (await reader.ReadAsync(ct))
            stored[reader.GetString(0)] = reader.GetString(1);
        return keys.Count(key => !stored.TryGetValue(key.FriendlyName, out var xml) || xml != key.Xml);
    }

    /// <summary>Compares each table's SQLite rows with PostgreSQL's: on a dry run only rows already present and different
    /// diverge; after the copy every legacy row must be present and identical, and PostgreSQL may hold no other row.</summary>
    private static LegacySqliteImportReport Report(
        LegacySqliteImportStatus status, IReadOnlyDictionary<string, IReadOnlyList<LegacyRow>> legacy,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> stored, IReadOnlyDictionary<string, int>? copied,
        BlobCheck blobs, IReadOnlyList<LegacyKey> keys, int keysCopied, int keysMissing)
    {
        var tables = new List<LegacySqliteTableReport>();
        foreach (var table in Tables)
        {
            var rows = legacy[table.Name].OrderBy(row => row.Key, StringComparer.Ordinal).ToList();
            var present = stored[table.Name];
            var keysOfLegacy = rows.Select(row => row.Key).ToHashSet(StringComparer.Ordinal);
            var diverged = copied is null
                ? rows.Count(row => present.TryGetValue(row.Key, out var digest) && digest != row.Digest)
                : rows.Count(row => !present.TryGetValue(row.Key, out var digest) || digest != row.Digest)
                  + present.Keys.Count(key => !keysOfLegacy.Contains(key));
            tables.Add(new LegacySqliteTableReport
            {
                Name = table.Name,
                Rows = rows.Count,
                Copied = copied?[table.Name] ?? rows.Count(row => !present.ContainsKey(row.Key)),
                Diverged = diverged,
                Sha256 = Sha256(string.Join('\n', rows.Select(row => row.Digest))),
                PostgresSha256 = copied is null ? null
                    : Sha256(string.Join('\n', rows.Select(row => present.TryGetValue(row.Key, out var digest) ? digest : "missing"))),
            });
        }

        return new LegacySqliteImportReport
        {
            Status = status,
            Summary = "",
            Tables = tables,
            Rows = tables.Sum(table => table.Rows),
            Copied = tables.Sum(table => table.Copied),
            Diverged = tables.Sum(table => table.Diverged),
            Releases = legacy["releases"].Count,
            FileIds = blobs.FileIds,
            Blobs = blobs.Blobs,
            BlobsVerified = blobs.Verified,
            BlobsDiverged = blobs.Diverged,
            BlobsMissing = blobs.Missing,
            BlobsSha256 = blobs.Sha256,
            Keys = keys.Count,
            KeysCopied = keysCopied,
            KeysMissing = keysMissing,
        };
    }

    private static NpgsqlDbType DbType(Kind kind) => kind switch
    {
        Kind.Text => NpgsqlDbType.Text,
        Kind.Int32 => NpgsqlDbType.Integer,
        Kind.Int64 => NpgsqlDbType.Bigint,
        Kind.Uuid => NpgsqlDbType.Uuid,
        Kind.Instant => NpgsqlDbType.TimestampTz,
        _ => throw new InvalidOperationException($"Unknown column kind {kind}."),
    };

    /// <summary>Writes one value unambiguously: its kind, then its text, a string length-prefixed.</summary>
    private static string Canonical(object? value) => value switch
    {
        null => "n",
        string text => $"s{text.Length}:{text}",
        int number => $"i{number.ToString(CultureInfo.InvariantCulture)}",
        long number => $"l{number.ToString(CultureInfo.InvariantCulture)}",
        Guid id => $"u{id:D}",
        DateTimeOffset instant => $"t{instant.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture)}",
        _ => throw new InvalidOperationException($"Unknown value type {value.GetType().Name}."),
    };

    private static string Sha256(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    /// <summary>Moves the SQLite file and its journal files aside under one stamped name; the key and blob folders stay.</summary>
    private string Retire(string path)
    {
        SqliteConnection.ClearAllPools();
        var stamp = time.GetUtcNow().ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        var retired = $"{path}.imported-{stamp}";
        foreach (var suffix in new[] { "-wal", "-shm" })
        {
            if (File.Exists(path + suffix))
                File.Move(path + suffix, retired + suffix);
        }

        File.Move(path, retired);
        return Path.GetFileName(retired);
    }

    [LoggerMessage(EventId = 8271, Level = LogLevel.Information, Message = "Imported the SQLite store: {Copied} of {Rows} rows and {KeysCopied} of {Keys} keys copied, every row verified")]
    private static partial void LogImported(ILogger logger, int copied, int rows, int keysCopied, int keys);

    [LoggerMessage(EventId = 8272, Level = LogLevel.Warning, Message = "Refused to retire the SQLite store: {Diverged} rows differ, {KeysMissing} keys are missing and {BlobsDiverged} local blobs disagree with their SHA-256")]
    private static partial void LogRefused(ILogger logger, int diverged, int keysMissing, int blobsDiverged);

    private enum Kind
    {
        Text,
        Int32,
        Int64,
        Uuid,
        Instant,
    }

    private sealed record LegacyColumn(string Sqlite, string Postgres, Kind Kind, bool Nullable = false, bool Optional = false);

    private sealed record LegacyTable(string Name, LegacyColumn[] Columns);

    /// <summary>One row's values in its table's column order; its key is the first column's canonical form.</summary>
    private sealed record LegacyRow(object?[] Values)
    {
        public string Key => Canonical(Values[0]);

        public string Digest => Sha256(string.Join('\u001f', Values.Select(Canonical)));
    }

    private sealed record LegacyKey(string FriendlyName, string Xml);

    private sealed record BlobCheck(int Blobs, int Verified, int Diverged, int Missing, int FileIds, string Sha256);
}
