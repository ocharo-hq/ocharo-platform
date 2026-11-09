using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace OcharoCatalogue.Persistence.Import;

/// <summary>Copies the retired SQLite store and its file key ring into the <c>catalogue</c> schema once, verifies the copy
/// row by row, then retires the SQLite file so the copy never runs again.</summary>
/// <remarks>
///   - every row keeps its id, revision, hashes, Files ids and instants; a row's canonical content is its columns in
///     order, an instant as its UTC moment and bytes as their SHA-256, and its digest is the SHA-256 of that
///   - one PostgreSQL transaction: a row PostgreSQL already holds with other content, or a row breaking a key, refuses the
///     run before anything commits, and the SQLite file stays where it is
///   - idempotent: a row or key already copied identically is kept; a rerun after a failed retire copies nothing and only
///     retires the file, and a run after the retire finds nothing to copy
///   - the same code serves dev and prod; the report carries counts and digests only, never rows, ids or keys
/// </remarks>
public sealed partial class LegacySqliteImport(
    NpgsqlDataSource dataSource, LegacySqliteSettings settings, TimeProvider time, ILogger<LegacySqliteImport> logger)
{
    private const string KeysTable = "data_protection_keys";
    private static readonly DateTimeOffsetToBinaryConverter BinaryInstant = new();

    /// <summary>Copies, verifies and retires; with <paramref name="dryRun"/> it reads both sides and writes nothing.</summary>
    public async Task<LegacySqliteImportReport> RunAsync(bool dryRun, CancellationToken ct)
    {
        var path = Path.GetFullPath(settings.Path);
        if (!File.Exists(path))
            return new LegacySqliteImportReport { Status = LegacySqliteImportStatus.Current, Summary = "No SQLite store to copy." };

        IReadOnlyList<LegacyTableRows> tables;
        try
        {
            tables = await ReadLegacyAsync(path, ct);
        }
        catch (LegacyShapeException failure)
        {
            return Refused(failure.Message);
        }

        var keys = ReadKeys(Path.GetFullPath(settings.KeysPath));

        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        var before = await ReadPresentAsync(connection, transaction, tables, ct);
        var keysBefore = await ReadPresentKeysAsync(connection, transaction, keys, ct);
        var diverged = Diverged(tables, before) + keys.Count(key => keysBefore.TryGetValue(key.Name, out var digest) && digest != key.Digest);

        if (dryRun || diverged > 0)
        {
            await transaction.RollbackAsync(ct);
            var planned = Report(tables, keys, before, keysBefore, copied: null) with
            {
                Status = diverged > 0 ? LegacySqliteImportStatus.Refused : LegacySqliteImportStatus.Planned,
                Summary = diverged > 0
                    ? $"{diverged} rows differ in PostgreSQL; nothing was written and the SQLite store stays in place."
                    : $"Dry run: nothing written; {Missing(tables, before) + keys.Count(key => !keysBefore.ContainsKey(key.Name))} rows would be copied.",
            };
            if (diverged > 0) LogRefused(logger, diverged);
            return planned;
        }

        var copied = new Dictionary<string, int>(StringComparer.Ordinal);
        try
        {
            foreach (var table in tables)
                copied[table.Table.Name] = await InsertAsync(connection, transaction, table, before[table.Table.Name], ct);
            copied[KeysTable] = await InsertKeysAsync(connection, transaction, keys, keysBefore, ct);
        }
        catch (PostgresException failure) when (failure.SqlState is PostgresErrorCodes.ForeignKeyViolation or PostgresErrorCodes.UniqueViolation)
        {
            await transaction.RollbackAsync(ct);
            LogRefused(logger, 1);
            return Refused($"A row breaks {failure.ConstraintName ?? "a key"} in PostgreSQL; nothing was written and the SQLite store stays in place.");
        }

        var during = await ReadPresentAsync(connection, transaction, tables, ct);
        var keysDuring = await ReadPresentKeysAsync(connection, transaction, keys, ct);
        if (Unverified(tables, during) + keys.Count(key => !keysDuring.TryGetValue(key.Name, out var digest) || digest != key.Digest) > 0)
        {
            await transaction.RollbackAsync(ct);
            return Refused("The copy did not read back identically; nothing was written and the SQLite store stays in place.");
        }

        await transaction.CommitAsync(ct);

        // Read back once more outside the transaction: what other sessions now see is what the report vouches for.
        var after = await ReadPresentAsync(connection, null, tables, ct);
        var keysAfter = await ReadPresentKeysAsync(connection, null, keys, ct);
        var report = Report(tables, keys, after, keysAfter, copied);
        var unverified = Unverified(tables, after) + keys.Count(key => !keysAfter.TryGetValue(key.Name, out var digest) || digest != key.Digest);
        if (unverified > 0)
        {
            LogRefused(logger, unverified);
            return report with
            {
                Status = LegacySqliteImportStatus.Refused,
                Summary = $"{unverified} rows did not read back identically; the SQLite store stays in place.",
            };
        }

        var retiredTo = Retire(path);
        var rows = tables.Sum(table => table.Rows.Count);
        var inserted = copied.Values.Sum();
        LogImported(logger, inserted, rows + keys.Count);
        return report with
        {
            Summary = $"Copied {inserted} of {rows + keys.Count} rows and keys; every row verified.",
            RetiredTo = retiredTo,
        };
    }

    private static LegacySqliteImportReport Refused(string summary) =>
        new() { Status = LegacySqliteImportStatus.Refused, Summary = summary };

    /// <summary>Reads every table in one read transaction, so a writer still running beside the import cannot tear the
    /// snapshot.</summary>
    private static async Task<IReadOnlyList<LegacyTableRows>> ReadLegacyAsync(string path, CancellationToken ct)
    {
        var builder = new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadWrite, Pooling = false };
        await using var connection = new SqliteConnection(builder.ConnectionString);
        await connection.OpenAsync(ct);
        await using var transaction = connection.BeginTransaction(deferred: true);
        var tables = new List<LegacyTableRows>();
        foreach (var table in LegacySqliteTables.All)
        {
            var columns = await ColumnsAsync(connection, transaction, table.Name, ct);
            if (columns.Count == 0)
            {
                tables.Add(new LegacyTableRows(table, []));
                continue;
            }

            var select = table.Columns.Select(column =>
                columns.Contains(column.Sqlite) ? $"\"{column.Sqlite}\""
                : column.IsNullable ? "NULL"
                : throw new LegacyShapeException($"The SQLite store's {table.Name} table lacks {column.Sqlite}."));
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"SELECT {string.Join(", ", select)} FROM \"{table.Name}\"";
            await using var reader = await command.ExecuteReaderAsync(ct);
            var rows = new List<LegacyRow>();
            while (await reader.ReadAsync(ct))
            {
                var values = new object?[table.Columns.Count];
                for (var index = 0; index < values.Length; index++)
                    values[index] = ReadSqlite(reader, index, table.Columns[index], table.Name);
                rows.Add(new LegacyRow(KeyText(values[0]), values, Digest(table, values)));
            }

            tables.Add(new LegacyTableRows(table, rows));
        }

        return tables;
    }

    private static async Task<HashSet<string>> ColumnsAsync(SqliteConnection connection, SqliteTransaction transaction, string table, CancellationToken ct)
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

    /// <summary>Reads one SQLite value as the store wrote it: EF's text GUIDs, binary-long instants and 0/1 flags.</summary>
    private static object? ReadSqlite(SqliteDataReader reader, int index, LegacyColumn column, string table)
    {
        if (reader.IsDBNull(index))
        {
            return column.IsNullable
                ? null
                : throw new LegacyShapeException($"The SQLite store's {table}.{column.Sqlite} holds a null.");
        }

        var stored = reader.GetFieldType(index);
        return column.Kind switch
        {
            LegacyColumnKind.Text => reader.GetString(index),
            LegacyColumnKind.Integer => checked((long)(int)reader.GetInt64(index)),
            LegacyColumnKind.BigInteger => reader.GetInt64(index),
            LegacyColumnKind.Boolean => reader.GetInt64(index) != 0,
            LegacyColumnKind.Uuid => stored == typeof(byte[]) ? new Guid(reader.GetFieldValue<byte[]>(index)) : Guid.Parse(reader.GetString(index)),
            LegacyColumnKind.Instant => stored == typeof(string)
                ? DateTimeOffset.Parse(reader.GetString(index), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToUniversalTime()
                : ((DateTimeOffset)BinaryInstant.ConvertFromProvider(reader.GetInt64(index))!).ToUniversalTime(),
            LegacyColumnKind.Bytes => reader.GetFieldValue<byte[]>(index),
            _ => throw new ArgumentOutOfRangeException(nameof(column)),
        };
    }

    private static object? ReadPostgres(NpgsqlDataReader reader, int index, LegacyColumnKind kind)
    {
        if (reader.IsDBNull(index)) return null;
        return kind switch
        {
            LegacyColumnKind.Text => reader.GetString(index),
            LegacyColumnKind.Integer => (long)reader.GetInt32(index),
            LegacyColumnKind.BigInteger => reader.GetInt64(index),
            LegacyColumnKind.Boolean => reader.GetBoolean(index),
            LegacyColumnKind.Uuid => reader.GetGuid(index),
            LegacyColumnKind.Instant => reader.GetFieldValue<DateTimeOffset>(index).ToUniversalTime(),
            LegacyColumnKind.Bytes => reader.GetFieldValue<byte[]>(index),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }

    private static IReadOnlyList<LegacyKey> ReadKeys(string folder) =>
        Directory.Exists(folder)
            ? Directory.GetFiles(folder, "*.xml")
                .Order(StringComparer.Ordinal)
                .Select(file =>
                {
                    var name = Path.GetFileNameWithoutExtension(file);
                    var xml = File.ReadAllText(file);
                    return new LegacyKey(name, xml, KeyDigest(name, xml));
                })
                .ToList()
            : [];

    /// <summary>Reads back PostgreSQL's copy of every SQLite row, by key: its canonical digest.</summary>
    private static async Task<Dictionary<string, Dictionary<string, string>>> ReadPresentAsync(
        NpgsqlConnection connection, NpgsqlTransaction? transaction, IReadOnlyList<LegacyTableRows> tables, CancellationToken ct)
    {
        var present = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        foreach (var (table, rows) in tables)
        {
            var found = new Dictionary<string, string>(StringComparer.Ordinal);
            present[table.Name] = found;
            if (rows.Count == 0) continue;

            await using var command = new NpgsqlCommand(
                $"SELECT {string.Join(", ", table.Columns.Select(column => column.Postgres))} "
                + $"FROM {CatalogueDatabaseConstants.Schema}.{table.Name} WHERE {table.Key.Postgres} = ANY($1)",
                connection, transaction);
            command.Parameters.Add(table.Key.Kind == LegacyColumnKind.Uuid
                ? new NpgsqlParameter { Value = rows.Select(row => (Guid)row.Values[0]!).ToArray() }
                : new NpgsqlParameter { Value = rows.Select(row => (string)row.Values[0]!).ToArray() });
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var values = new object?[table.Columns.Count];
                for (var index = 0; index < values.Length; index++)
                    values[index] = ReadPostgres(reader, index, table.Columns[index].Kind);
                found[KeyText(values[0])] = Digest(table, values);
            }
        }

        return present;
    }

    private static async Task<Dictionary<string, string>> ReadPresentKeysAsync(
        NpgsqlConnection connection, NpgsqlTransaction? transaction, IReadOnlyList<LegacyKey> keys, CancellationToken ct)
    {
        var found = new Dictionary<string, string>(StringComparer.Ordinal);
        if (keys.Count == 0) return found;
        await using var command = new NpgsqlCommand(
            $"SELECT friendly_name, xml FROM {CatalogueDatabaseConstants.Schema}.{KeysTable} WHERE friendly_name = ANY($1)",
            connection, transaction);
        command.Parameters.AddWithValue(keys.Select(key => key.Name).ToArray());
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            found[reader.GetString(0)] = KeyDigest(reader.GetString(0), reader.GetString(1));
        return found;
    }

    private static async Task<int> InsertAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, LegacyTableRows table, IReadOnlyDictionary<string, string> present,
        CancellationToken ct)
    {
        var columns = table.Table.Columns;
        var sql = $"INSERT INTO {CatalogueDatabaseConstants.Schema}.{table.Table.Name} "
            + $"({string.Join(", ", columns.Select(column => column.Postgres))}) "
            + $"VALUES ({string.Join(", ", columns.Select((_, index) => $"${index + 1}"))}) "
            + $"ON CONFLICT ({table.Table.Key.Postgres}) DO NOTHING";
        var copied = 0;
        foreach (var row in table.Rows.Where(row => !present.ContainsKey(row.Key)))
        {
            await using var insert = new NpgsqlCommand(sql, connection, transaction);
            for (var index = 0; index < columns.Count; index++)
            {
                var value = row.Values[index];
                insert.Parameters.Add(new NpgsqlParameter
                {
                    NpgsqlDbType = columns[index].DbType,
                    Value = value switch
                    {
                        null => DBNull.Value,
                        long number when columns[index].Kind == LegacyColumnKind.Integer => checked((int)number),
                        _ => value,
                    },
                });
            }

            copied += await insert.ExecuteNonQueryAsync(ct);
        }

        return copied;
    }

    private static async Task<int> InsertKeysAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, IReadOnlyList<LegacyKey> keys, IReadOnlyDictionary<string, string> present,
        CancellationToken ct)
    {
        var copied = 0;
        foreach (var key in keys.Where(key => !present.ContainsKey(key.Name)))
        {
            await using var insert = new NpgsqlCommand(
                $"INSERT INTO {CatalogueDatabaseConstants.Schema}.{KeysTable} (friendly_name, xml) VALUES ($1, $2) "
                + "ON CONFLICT (friendly_name) DO NOTHING",
                connection, transaction);
            insert.Parameters.AddWithValue(key.Name);
            insert.Parameters.AddWithValue(key.Xml);
            copied += await insert.ExecuteNonQueryAsync(ct);
        }

        return copied;
    }

    private static int Diverged(IReadOnlyList<LegacyTableRows> tables, IReadOnlyDictionary<string, Dictionary<string, string>> present) =>
        tables.Sum(table => table.Rows.Count(row => present[table.Table.Name].TryGetValue(row.Key, out var digest) && digest != row.Digest));

    private static int Missing(IReadOnlyList<LegacyTableRows> tables, IReadOnlyDictionary<string, Dictionary<string, string>> present) =>
        tables.Sum(table => table.Rows.Count(row => !present[table.Table.Name].ContainsKey(row.Key)));

    private static int Unverified(IReadOnlyList<LegacyTableRows> tables, IReadOnlyDictionary<string, Dictionary<string, string>> present) =>
        tables.Sum(table => table.Rows.Count(row => !present[table.Table.Name].TryGetValue(row.Key, out var digest) || digest != row.Digest));

    /// <summary>Builds the report; <paramref name="copied"/> null is a plan, which counts the rows PostgreSQL lacks and
    /// reads nothing back.</summary>
    private static LegacySqliteImportReport Report(
        IReadOnlyList<LegacyTableRows> tables, IReadOnlyList<LegacyKey> keys, IReadOnlyDictionary<string, Dictionary<string, string>> present,
        IReadOnlyDictionary<string, string> keysPresent, IReadOnlyDictionary<string, int>? copied)
    {
        var reports = tables.Select(table =>
        {
            var stored = present[table.Table.Name];
            var ordered = table.Rows.OrderBy(row => row.Key, StringComparer.Ordinal).ToList();
            return new LegacyTableReport(
                table.Table.Name,
                table.Rows.Count,
                copied?.GetValueOrDefault(table.Table.Name) ?? ordered.Count(row => !stored.ContainsKey(row.Key)),
                ordered.Count(row => stored.TryGetValue(row.Key, out var digest) && digest != row.Digest),
                Sha256(string.Join('\n', ordered.Select(row => row.Digest))),
                copied is null ? null : Sha256(string.Join('\n', ordered.Select(row => stored.GetValueOrDefault(row.Key, "missing")))));
        }).ToList();
        var orderedKeys = keys.OrderBy(key => key.Name, StringComparer.Ordinal).ToList();
        var keyReport = new LegacyTableReport(
            KeysTable,
            keys.Count,
            copied?.GetValueOrDefault(KeysTable) ?? keys.Count(key => !keysPresent.ContainsKey(key.Name)),
            keys.Count(key => keysPresent.TryGetValue(key.Name, out var digest) && digest != key.Digest),
            Sha256(string.Join('\n', orderedKeys.Select(key => key.Digest))),
            copied is null ? null : Sha256(string.Join('\n', orderedKeys.Select(key => keysPresent.GetValueOrDefault(key.Name, "missing")))));
        return new LegacySqliteImportReport
        {
            Status = LegacySqliteImportStatus.Imported,
            Summary = string.Empty,
            Tables = reports,
            Keys = keyReport,
            FileIds = tables.Sum(table =>
            {
                var index = table.Table.Columns.ToList().FindIndex(column => column.Sqlite == "FileId");
                return index < 0 ? 0 : table.Rows.Count(row => row.Values[index] is string { Length: > 0 });
            }),
            Publications = tables.Single(table => table.Table.Name == "catalogue_publications").Rows.Count,
        };
    }

    private static string KeyText(object? key) => key switch
    {
        Guid id => id.ToString("D"),
        string text => text,
        _ => throw new LegacyShapeException("A key is empty."),
    };

    /// <summary>The SHA-256 of a row's canonical content: a JSON array of its values in column order, an instant as its UTC
    /// moment to the tick, bytes as their SHA-256.</summary>
    private static string Digest(LegacyTable table, object?[] values)
    {
        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartArray();
            json.WriteStringValue(table.Name);
            foreach (var value in values)
            {
                switch (value)
                {
                    case null: json.WriteNullValue(); break;
                    case string text: json.WriteStringValue(text); break;
                    case long number: json.WriteNumberValue(number); break;
                    case bool flag: json.WriteBooleanValue(flag); break;
                    case Guid id: json.WriteStringValue(id.ToString("D")); break;
                    case DateTimeOffset instant:
                        json.WriteStringValue(instant.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture));
                        break;
                    case byte[] bytes: json.WriteStringValue("sha256:" + Convert.ToHexStringLower(SHA256.HashData(bytes))); break;
                    default: throw new InvalidOperationException($"No canonical form for {value.GetType().Name}.");
                }
            }

            json.WriteEndArray();
        }

        return Convert.ToHexStringLower(SHA256.HashData(buffer.ToArray()));
    }

    private static string KeyDigest(string name, string xml) => Sha256($"{name}\n{Sha256(xml)}");

    private static string Sha256(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    /// <summary>Moves the SQLite file and its journal files aside under one stamped name; the key folder and the blob
    /// folder stay where they are.</summary>
    private string Retire(string path)
    {
        var stamp = time.GetUtcNow().ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        var retired = $"{path}.imported-{stamp}";
        SqliteConnection.ClearAllPools();
        foreach (var suffix in new[] { "-wal", "-shm" })
        {
            if (File.Exists(path + suffix))
                File.Move(path + suffix, retired + suffix);
        }

        File.Move(path, retired);
        return Path.GetFileName(retired);
    }

    [LoggerMessage(EventId = 8278, Level = LogLevel.Information, Message = "Imported the SQLite store: {Copied} of {Rows} rows and keys copied, every row verified")]
    private static partial void LogImported(ILogger logger, int copied, int rows);

    [LoggerMessage(EventId = 8279, Level = LogLevel.Warning, Message = "Refused to retire the SQLite store: {Rows} rows differ or break a key in PostgreSQL")]
    private static partial void LogRefused(ILogger logger, int rows);

    private sealed record LegacyRow(string Key, object?[] Values, string Digest);

    private sealed record LegacyTableRows(LegacyTable Table, IReadOnlyList<LegacyRow> Rows);

    private sealed record LegacyKey(string Name, string Xml, string Digest);

    private sealed class LegacyShapeException(string message) : Exception(message);
}
