using System.Data.Common;
using BrandWorkspace.Application.Events.Models;
using BrandWorkspace.Application.Events.Repositories;
using BrandWorkspace.Application.Foundation.Errors;
using BrandWorkspace.Domain.Events.Models;
using Dapper;
using Npgsql;
using WoW.Two.Sdk.Beta.Be.Data.Abstractions;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace BrandWorkspace.Persistence.Events.Repositories;

/// <summary>Accesses the showcase events inside one organization.</summary>
public sealed class EventRepository(IDbConnectionFactory connectionFactory, TimeProvider time)
    : IEventRepository, IEventRecordsReader
{
    private const string EventColumns =
        "id, import_key, title, organizer, scope, format, country, city, venue, starts_on, ends_on, date_precision, "
        + "application_deadline, participation, cost, website, source_url, status, priority, notes, revision, "
        + "created_at, updated_at";

    private const string DuplicateKey = "An event with this import key already exists.";

    public async Task<IReadOnlyList<ShowcaseEvent>> GetEventsAsync(
        Guid organizationId,
        ShowcaseEventFilter filter,
        int limit,
        int offset,
        CancellationToken token)
    {
        // An undated event has no start date; it matches a date range only when undated events are included.
        const string sql =
            $"""
             SELECT {EventColumns}
             FROM events.showcase_events
             WHERE organization_id=@organizationId
                 AND (@query::text IS NULL
                     OR title ILIKE @query ESCAPE '\' OR organizer ILIKE @query ESCAPE '\'
                     OR city ILIKE @query ESCAPE '\' OR venue ILIKE @query ESCAPE '\'
                     OR country ILIKE @query ESCAPE '\' OR import_key ILIKE @query ESCAPE '\')
                 AND (@scope::text IS NULL OR scope=@scope::text)
                 AND (@format::text IS NULL OR format=@format::text)
                 AND (@status::text IS NULL OR status=@status::text)
                 AND (starts_on IS NULL AND @includeUndated
                     OR starts_on IS NOT NULL
                         AND (@from::date IS NULL
                             OR coalesce(ends_on, CASE WHEN date_precision IN ('month', 'estimated')
                                 THEN (date_trunc('month', starts_on) + interval '1 month - 1 day')::date
                                 ELSE starts_on END) >= @from::date)
                         AND (@to::date IS NULL OR starts_on <= @to::date))
             ORDER BY starts_on ASC NULLS LAST, lower(title), id
             LIMIT @limit OFFSET @offset
             """;
        var parameters = new
        {
            organizationId,
            query = filter.Query is null ? null : "%" + EscapeLike(filter.Query) + "%",
            scope = filter.Scope,
            format = filter.Format,
            status = filter.Status,
            from = filter.From,
            to = filter.To,
            includeUndated = filter.IncludeUndated,
            limit,
            offset,
        };
        await using var connection = await connectionFactory.CreateOpenAsync(token);
        IEnumerable<EventRow> rows = await connection.QueryAsync<EventRow>(Command(sql, parameters, token));
        return rows.Select(row => row.ToEvent()).ToList();
    }

    public async Task<ShowcaseEventSummary> GetSummaryAsync(
        Guid organizationId,
        DateOnly today,
        int upcomingLimit,
        CancellationToken token)
    {
        const string countsSql =
            """
            SELECT status, count(*)::int AS count
            FROM events.showcase_events
            WHERE organization_id=@organizationId
            GROUP BY status
            """;
        const string deadlinesSql =
            $"""
             SELECT {EventColumns}
             FROM events.showcase_events
             WHERE organization_id=@organizationId
                 AND application_deadline >= @today::date
                 AND status NOT IN ('skipped', 'rejected')
             ORDER BY application_deadline, starts_on NULLS LAST, lower(title), id
             LIMIT @upcomingLimit
             """;
        await using var connection = await connectionFactory.CreateOpenAsync(token);
        IEnumerable<StatusCountRow> counts = await connection.QueryAsync<StatusCountRow>(
            Command(countsSql, new { organizationId }, token));
        var byStatus = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (StatusCountRow count in counts) byStatus[count.Status] = count.Count;
        IEnumerable<EventRow> upcoming = await connection.QueryAsync<EventRow>(
            Command(deadlinesSql, new { organizationId, today, upcomingLimit }, token));
        return new ShowcaseEventSummary(byStatus, upcoming.Select(row => row.ToEvent()).ToList());
    }

    public async Task<Result<ShowcaseEvent>> GetEventAsync(Guid organizationId, Guid eventId, CancellationToken token)
    {
        const string sql =
            $"SELECT {EventColumns} FROM events.showcase_events WHERE organization_id=@organizationId AND id=@eventId";
        await using var connection = await connectionFactory.CreateOpenAsync(token);
        EventRow? row = await connection.QueryFirstOrDefaultAsync<EventRow>(
            Command(sql, new { organizationId, eventId }, token));
        return row is null ? Result<ShowcaseEvent>.Fail(NotFound("event")) : Result<ShowcaseEvent>.Ok(row.ToEvent());
    }

    public async Task<ShowcaseEvent?> FindEventByKeyAsync(Guid organizationId, string key, CancellationToken token)
    {
        const string sql =
            $"""
             SELECT {EventColumns} FROM events.showcase_events
             WHERE organization_id=@organizationId AND import_key=@key
             """;
        await using var connection = await connectionFactory.CreateOpenAsync(token);
        EventRow? row = await connection.QueryFirstOrDefaultAsync<EventRow>(
            Command(sql, new { organizationId, key }, token));
        return row?.ToEvent();
    }

    public async Task<IReadOnlyList<ShowcaseEventRecord>> GetEventRecordsAsync(
        Guid organizationId, CancellationToken token)
    {
        const string sql =
            $"""
             SELECT {EventColumns}, created_by
             FROM events.showcase_events
             WHERE organization_id=@organizationId
             ORDER BY created_at, id
             """;
        await using var connection = await connectionFactory.CreateOpenAsync(token);
        IEnumerable<EventRow> rows = await connection.QueryAsync<EventRow>(Command(sql, new { organizationId }, token));
        return rows.Select(row => new ShowcaseEventRecord(row.ToEvent(), row.CreatedBy)).ToList();
    }

    public async Task<Result<ShowcaseEvent>> CreateEventAsync(
        Guid organizationId,
        string actor,
        string? key,
        ShowcaseEventInput input,
        CancellationToken token)
    {
        const string sql =
            $"""
             INSERT INTO events.showcase_events(organization_id, id, import_key, title, organizer, scope, format,
                 country, city, venue, starts_on, ends_on, date_precision, application_deadline, participation, cost,
                 website, source_url, status, priority, notes, revision, created_by, created_at, updated_at)
             VALUES (@organizationId, @id, @key::text, @title, @organizer::text, @scope, @format, @country::text,
                 @city::text, @venue::text, @startsOn::date, @endsOn::date, @datePrecision, @applicationDeadline::date,
                 @participation, @cost::text, @website::text, @sourceUrl::text, @status, @priority, @notes, 1, @actor,
                 @now, @now)
             RETURNING {EventColumns}
             """;
        var parameters = new
        {
            organizationId,
            id = Guid.NewGuid(),
            key,
            title = input.Title,
            organizer = input.Organizer,
            scope = input.Scope,
            format = input.Format,
            country = input.Country,
            city = input.City,
            venue = input.Venue,
            startsOn = input.StartsOn,
            endsOn = input.EndsOn,
            datePrecision = input.DatePrecision,
            applicationDeadline = input.ApplicationDeadline,
            participation = input.Participation,
            cost = input.Cost,
            website = input.Website,
            sourceUrl = input.SourceUrl,
            status = input.Status,
            priority = input.Priority,
            notes = input.Notes,
            actor,
            now = time.GetUtcNow(),
        };
        await using var connection = await connectionFactory.CreateOpenAsync(token);
        try
        {
            EventRow row = await connection.QueryFirstAsync<EventRow>(Command(sql, parameters, token));
            return Result<ShowcaseEvent>.Ok(row.ToEvent());
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return Result<ShowcaseEvent>.Fail(DeskErrors.Conflict(DuplicateKey));
        }
    }

    public async Task<Result<ShowcaseEvent>> UpdateEventAsync(
        Guid organizationId,
        Guid eventId,
        int expectedRevision,
        ShowcaseEventInput input,
        CancellationToken token)
    {
        const string sql =
            $"""
             UPDATE events.showcase_events
             SET title=@title, organizer=@organizer::text, scope=@scope, format=@format, country=@country::text,
                 city=@city::text, venue=@venue::text, starts_on=@startsOn::date, ends_on=@endsOn::date,
                 date_precision=@datePrecision, application_deadline=@applicationDeadline::date,
                 participation=@participation, cost=@cost::text, website=@website::text, source_url=@sourceUrl::text,
                 status=@status, priority=@priority, notes=@notes, revision=revision+1, updated_at=@now
             WHERE organization_id=@organizationId AND id=@eventId AND revision=@expectedRevision
             RETURNING {EventColumns}
             """;
        var parameters = new
        {
            organizationId,
            eventId,
            expectedRevision,
            title = input.Title,
            organizer = input.Organizer,
            scope = input.Scope,
            format = input.Format,
            country = input.Country,
            city = input.City,
            venue = input.Venue,
            startsOn = input.StartsOn,
            endsOn = input.EndsOn,
            datePrecision = input.DatePrecision,
            applicationDeadline = input.ApplicationDeadline,
            participation = input.Participation,
            cost = input.Cost,
            website = input.Website,
            sourceUrl = input.SourceUrl,
            status = input.Status,
            priority = input.Priority,
            notes = input.Notes,
            now = time.GetUtcNow(),
        };
        await using var connection = await connectionFactory.CreateOpenAsync(token);
        EventRow? row = await connection.QueryFirstOrDefaultAsync<EventRow>(Command(sql, parameters, token));
        if (row is not null) return Result<ShowcaseEvent>.Ok(row.ToEvent());
        AppError error = await MissingOrConflictAsync(connection, organizationId, eventId, "event", token);
        return Result<ShowcaseEvent>.Fail(error);
    }

    public async Task<Result> DeleteEventAsync(
        Guid organizationId,
        Guid eventId,
        int expectedRevision,
        CancellationToken token)
    {
        const string sql =
            """
            DELETE FROM events.showcase_events
            WHERE organization_id=@organizationId AND id=@eventId AND revision=@expectedRevision
            """;
        await using var connection = await connectionFactory.CreateOpenAsync(token);
        int deleted = await connection.ExecuteAsync(
            Command(sql, new { organizationId, eventId, expectedRevision }, token));
        if (deleted != 0) return Result.Ok();
        return Result.Fail(await MissingOrConflictAsync(connection, organizationId, eventId, "event", token));
    }

    /// <summary>Tells a missing event from one that changed since it was loaded.</summary>
    private static async Task<AppError> MissingOrConflictAsync(
        DbConnection connection,
        Guid organizationId,
        Guid id,
        string noun,
        CancellationToken token)
    {
        const string sql = "SELECT 1 FROM events.showcase_events WHERE organization_id=@organizationId AND id=@id";
        int? found = await connection.ExecuteScalarAsync<int?>(Command(sql, new { organizationId, id }, token));
        return found is null ? NotFound(noun) : Conflict(noun);
    }

    private static string EscapeLike(string value)
    {
        return value.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");
    }

    private static AppError NotFound(string noun)
    {
        return DeskErrors.NotFound($"The {noun} was not found.");
    }

    private static AppError Conflict(string noun)
    {
        return DeskErrors.Conflict($"The {noun} changed since it was loaded. Reload it before saving again.");
    }

    private static CommandDefinition Command(string sql, object parameters, CancellationToken token)
    {
        return new CommandDefinition(sql, parameters, cancellationToken: token);
    }

    /// <summary>One stored event, as its columns read.</summary>
    private sealed class EventRow
    {
        public Guid Id { get; init; }
        public string? ImportKey { get; init; }
        public string Title { get; init; } = string.Empty;
        public string? Organizer { get; init; }
        public string Scope { get; init; } = string.Empty;
        public string Format { get; init; } = string.Empty;
        public string? Country { get; init; }
        public string? City { get; init; }
        public string? Venue { get; init; }
        public DateOnly? StartsOn { get; init; }
        public DateOnly? EndsOn { get; init; }
        public string DatePrecision { get; init; } = string.Empty;
        public DateOnly? ApplicationDeadline { get; init; }
        public string Participation { get; init; } = string.Empty;
        public string? Cost { get; init; }
        public string? Website { get; init; }
        public string? SourceUrl { get; init; }
        public string Status { get; init; } = string.Empty;
        public string Priority { get; init; } = string.Empty;
        public string Notes { get; init; } = string.Empty;
        public int Revision { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
        public DateTimeOffset UpdatedAt { get; init; }

        /// <summary>Gets the author; read only by the export, which selects the column.</summary>
        public string CreatedBy { get; init; } = string.Empty;

        public ShowcaseEvent ToEvent()
        {
            return new ShowcaseEvent(
                Id,
                ImportKey,
                Title,
                Organizer,
                Scope,
                Format,
                Country,
                City,
                Venue,
                StartsOn,
                EndsOn,
                DatePrecision,
                ApplicationDeadline,
                Participation,
                Cost,
                Website,
                SourceUrl,
                Status,
                Priority,
                Notes,
                Revision,
                CreatedAt,
                UpdatedAt);
        }
    }

    /// <summary>How many events hold one status.</summary>
    private sealed class StatusCountRow
    {
        public string Status { get; init; } = string.Empty;
        public int Count { get; init; }
    }
}
