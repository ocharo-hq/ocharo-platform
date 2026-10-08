using Npgsql;
using NpgsqlTypes;
using BrandWorkspace.Application.Events.Models;
using BrandWorkspace.Application.Events.Repositories;
using BrandWorkspace.Application.Preparation.Models;
using BrandWorkspace.Domain.Events.Models;

namespace BrandWorkspace.Persistence.Events.Repositories;

/// <summary>Persists showcase events inside one organization in PostgreSQL.</summary>
public sealed class EventRepository(NpgsqlDataSource dataSource, TimeProvider time) : IEventRepository
{
    private const string EventColumns =
        "id, import_key, title, organizer, scope, format, country, city, venue, starts_on, ends_on, date_precision, " +
        "application_deadline, participation, cost, website, source_url, status, priority, notes, revision, created_at, updated_at";
    private const string DuplicateKey = "An event with this import key already exists.";

    public async Task<IReadOnlyList<ShowcaseEvent>> GetEventsAsync(
        Guid organizationId, ShowcaseEventFilter filter, int limit, int offset, CancellationToken token)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(token);
        // An undated event has no start date; it matches a date range only when undated events are included.
        await using NpgsqlCommand command = Command(connection, null, $"""
            SELECT {EventColumns}
            FROM events.showcase_events
            WHERE organization_id=$1
                AND ($2::text IS NULL
                    OR title ILIKE $2 ESCAPE '\' OR organizer ILIKE $2 ESCAPE '\' OR city ILIKE $2 ESCAPE '\'
                    OR venue ILIKE $2 ESCAPE '\' OR country ILIKE $2 ESCAPE '\' OR import_key ILIKE $2 ESCAPE '\')
                AND ($3::text IS NULL OR scope=$3::text)
                AND ($4::text IS NULL OR format=$4::text)
                AND ($5::text IS NULL OR status=$5::text)
                AND (starts_on IS NULL AND $8
                    OR starts_on IS NOT NULL
                        AND ($6::date IS NULL OR coalesce(ends_on, CASE WHEN date_precision IN ('month', 'estimated')
                            THEN (date_trunc('month', starts_on) + interval '1 month - 1 day')::date ELSE starts_on END) >= $6::date)
                        AND ($7::date IS NULL OR starts_on <= $7::date))
            ORDER BY starts_on ASC NULLS LAST, lower(title), id
            LIMIT $9 OFFSET $10
            """, organizationId, filter.Query is null ? null : "%" + EscapeLike(filter.Query) + "%", filter.Scope, filter.Format,
            filter.Status, filter.From, filter.To, filter.IncludeUndated, limit, offset);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(token);
        var events = new List<ShowcaseEvent>();
        while (await reader.ReadAsync(token)) events.Add(ReadEvent(reader));
        return events;
    }

    public async Task<ShowcaseEventSummary> GetSummaryAsync(
        Guid organizationId, DateOnly today, int upcomingLimit, CancellationToken token)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(token);
        var byStatus = new Dictionary<string, int>(StringComparer.Ordinal);
        await using (NpgsqlCommand counts = Command(connection, null,
            "SELECT status, count(*)::int FROM events.showcase_events WHERE organization_id=$1 GROUP BY status", organizationId))
        await using (NpgsqlDataReader reader = await counts.ExecuteReaderAsync(token))
        {
            while (await reader.ReadAsync(token)) byStatus[reader.GetString(0)] = reader.GetInt32(1);
        }
        var upcoming = new List<ShowcaseEvent>();
        await using (NpgsqlCommand deadlines = Command(connection, null, $"""
            SELECT {EventColumns}
            FROM events.showcase_events
            WHERE organization_id=$1 AND application_deadline >= $2::date AND status NOT IN ('skipped', 'rejected')
            ORDER BY application_deadline, starts_on NULLS LAST, lower(title), id
            LIMIT $3
            """, organizationId, today, upcomingLimit))
        await using (NpgsqlDataReader reader = await deadlines.ExecuteReaderAsync(token))
        {
            while (await reader.ReadAsync(token)) upcoming.Add(ReadEvent(reader));
        }
        return new ShowcaseEventSummary(byStatus, upcoming);
    }

    public async Task<ShowcaseEvent> GetEventAsync(Guid organizationId, Guid eventId, CancellationToken token)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(token);
        await using NpgsqlCommand command = Command(connection, null,
            $"SELECT {EventColumns} FROM events.showcase_events WHERE organization_id=$1 AND id=$2", organizationId, eventId);
        return await ReadOptionalAsync(command, ReadEvent, token) ?? throw NotFound("event");
    }

    public async Task<ShowcaseEvent?> FindEventByKeyAsync(Guid organizationId, string key, CancellationToken token)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(token);
        await using NpgsqlCommand command = Command(connection, null,
            $"SELECT {EventColumns} FROM events.showcase_events WHERE organization_id=$1 AND import_key=$2", organizationId, key);
        return await ReadOptionalAsync(command, ReadEvent, token);
    }

    public async Task<ShowcaseEvent> CreateEventAsync(
        Guid organizationId, string actor, string? key, ShowcaseEventInput input, CancellationToken token)
    {
        DateTimeOffset now = time.GetUtcNow();
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(token);
        await using NpgsqlCommand insert = Command(connection, null, $"""
            INSERT INTO events.showcase_events(organization_id, id, import_key, title, organizer, scope, format, country, city,
                venue, starts_on, ends_on, date_precision, application_deadline, participation, cost, website, source_url, status,
                priority, notes, revision, created_by, created_at, updated_at)
            VALUES ($1, $2, $3::text, $4, $5::text, $6, $7, $8::text, $9::text, $10::text, $11::date, $12::date, $13, $14::date,
                $15, $16::text, $17::text, $18::text, $19, $20, $21, 1, $22, $23, $23)
            RETURNING {EventColumns}
            """, organizationId, Guid.NewGuid(), key, input.Title, input.Organizer, input.Scope, input.Format, input.Country,
            input.City, input.Venue, input.StartsOn, input.EndsOn, input.DatePrecision, input.ApplicationDeadline,
            input.Participation, input.Cost, input.Website, input.SourceUrl, input.Status, input.Priority, input.Notes, actor, now);
        return await ReadSingleAsync(insert, ReadEvent, token, DuplicateKey);
    }

    public async Task<ShowcaseEvent> UpdateEventAsync(
        Guid organizationId, Guid eventId, int expectedRevision, ShowcaseEventInput input, CancellationToken token)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(token);
        await using NpgsqlCommand update = Command(connection, null, $"""
            UPDATE events.showcase_events
            SET title=$4, organizer=$5::text, scope=$6, format=$7, country=$8::text, city=$9::text, venue=$10::text,
                starts_on=$11::date, ends_on=$12::date, date_precision=$13, application_deadline=$14::date, participation=$15,
                cost=$16::text, website=$17::text, source_url=$18::text, status=$19, priority=$20, notes=$21,
                revision=revision+1, updated_at=$22
            WHERE organization_id=$1 AND id=$2 AND revision=$3
            RETURNING {EventColumns}
            """, organizationId, eventId, expectedRevision, input.Title, input.Organizer, input.Scope, input.Format, input.Country,
            input.City, input.Venue, input.StartsOn, input.EndsOn, input.DatePrecision, input.ApplicationDeadline,
            input.Participation, input.Cost, input.Website, input.SourceUrl, input.Status, input.Priority, input.Notes,
            time.GetUtcNow());
        return await ReadOptionalAsync(update, ReadEvent, token)
            ?? throw await MissingOrConflictAsync(connection, null, "showcase_events", organizationId, eventId, "event", token);
    }

    public async Task DeleteEventAsync(Guid organizationId, Guid eventId, int expectedRevision, CancellationToken token)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(token);
        await using NpgsqlCommand delete = Command(connection, null,
            "DELETE FROM events.showcase_events WHERE organization_id=$1 AND id=$2 AND revision=$3",
            organizationId, eventId, expectedRevision);
        if (await delete.ExecuteNonQueryAsync(token) == 0)
            throw await MissingOrConflictAsync(connection, null, "showcase_events", organizationId, eventId, "event", token);
    }

    private static ShowcaseEvent ReadEvent(NpgsqlDataReader reader) => new(
        reader.GetGuid(0),
        NullableString(reader, 1),
        reader.GetString(2),
        NullableString(reader, 3),
        reader.GetString(4),
        reader.GetString(5),
        NullableString(reader, 6),
        NullableString(reader, 7),
        NullableString(reader, 8),
        NullableDate(reader, 9),
        NullableDate(reader, 10),
        reader.GetString(11),
        NullableDate(reader, 12),
        reader.GetString(13),
        NullableString(reader, 14),
        NullableString(reader, 15),
        NullableString(reader, 16),
        reader.GetString(17),
        reader.GetString(18),
        reader.GetString(19),
        reader.GetInt32(20),
        reader.GetFieldValue<DateTimeOffset>(21),
        reader.GetFieldValue<DateTimeOffset>(22));

    private static async Task<PreparationException> MissingOrConflictAsync(
        NpgsqlConnection connection, NpgsqlTransaction? transaction, string table, Guid organizationId, Guid id, string noun,
        CancellationToken token)
    {
        await using NpgsqlCommand command = Command(connection, transaction,
            $"SELECT 1 FROM events.{table} WHERE organization_id=$1 AND id=$2", organizationId, id);
        return await command.ExecuteScalarAsync(token) is null ? NotFound(noun) : Conflict(noun);
    }

    private static async Task<T> ReadSingleAsync<T>(
        NpgsqlCommand command, Func<NpgsqlDataReader, T> read, CancellationToken token, string? duplicateMessage = null)
        where T : class
    {
        return await ReadOptionalAsync(command, read, token, duplicateMessage)
            ?? throw new InvalidOperationException("The statement returned no row.");
    }

    private static async Task<T?> ReadOptionalAsync<T>(
        NpgsqlCommand command, Func<NpgsqlDataReader, T> read, CancellationToken token, string? duplicateMessage = null)
        where T : class
    {
        try
        {
            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(token);
            return await reader.ReadAsync(token) ? read(reader) : null;
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation && duplicateMessage is not null)
        {
            throw new PreparationException("conflict", duplicateMessage);
        }
    }

    private static string? NullableString(NpgsqlDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    private static DateOnly? NullableDate(NpgsqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetFieldValue<DateOnly>(ordinal);

    private static string EscapeLike(string value) => value.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");

    private static PreparationException NotFound(string noun) => new("not-found", $"The {noun} was not found.");

    private static PreparationException Conflict(string noun) =>
        new("conflict", $"The {noun} changed since it was loaded. Reload it before saving again.");

    private static NpgsqlCommand Command(
        NpgsqlConnection connection, NpgsqlTransaction? transaction, string sql, params object?[] values)
    {
        var command = new NpgsqlCommand(sql, connection, transaction);
        foreach (object? value in values)
        {
            if (value is null)
                command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Unknown, Value = DBNull.Value });
            else command.Parameters.AddWithValue(value);
        }
        return command;
    }
}
