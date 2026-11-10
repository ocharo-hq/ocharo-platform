using BrandWorkspace.Application.Events.Models;
using BrandWorkspace.Application.Events.Repositories;
using BrandWorkspace.Application.Foundation.Errors;
using BrandWorkspace.Domain.Events.Models;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Beta.Be.Events;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Tracking;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace BrandWorkspace.Infrastructure.Events.Services;

/// <summary>Holds which WoW2 Events catalog serves Desk's Events page.</summary>
public sealed class EventsServiceOptions
{
    public const string SectionName = "Events";

    /// <summary>
    /// Gets or sets the Events service's root URL, which serves Desk's Events page. A deployed run refuses to start
    /// without it; a local run without it answers 503 on the Events routes. Desk keeps no events store of its own since
    /// v0.3 Iteration 5, so there is no fallback.
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>Gets or sets the catalog slug; Ocharo's intake and import write <c>ocharo-fashion</c>.</summary>
    public string Catalog { get; set; } = "ocharo-fashion";
}

/// <summary>
/// Serves Desk's events port from WoW2 Events (events-adoption.md #7, #8): reads are the organization's tracking list of
/// the catalog, a new event is a member proposal, stage, priority, notes, participation and cost are tracking; fact
/// edits and deletes moved to Events' review and answer Gone.
/// </summary>
/// <remarks>
/// Every call acts for the signed-in person through the brokers' token exchange, so Events scopes the tracking to the
/// session's organization, the one Desk's organization filter already matched against the route. Desk's filters,
/// order and paging apply in memory over the whole catalog, the same rules as Desk's own SQL; P1 catalogs hold hundreds
/// of events, and pushing filters to Events' query waits for the fe package.
/// </remarks>
public sealed class EventsServiceRepository(
    ICatalogEventsBroker catalogEvents,
    IEventTrackingBroker tracking,
    IOptions<EventsServiceOptions> options) : IEventRepository
{
    /// <summary>Holds the most pages one read follows, a guard against a cursor that never ends.</summary>
    private const int MaximumPages = 50;

    private static readonly HashSet<string> ClosedStatuses = new(StringComparer.Ordinal) { "skipped", "rejected" };

    /// <summary>Gets the catalog slug; unconfigured, every call answers 503 before reaching Events.</summary>
    private string Catalog => string.IsNullOrWhiteSpace(options.Value.BaseUrl)
        ? throw DeskErrors.Unavailable("WoW2 Events is not configured for this Desk (Events:BaseUrl).").ToException()
        : options.Value.Catalog;

    public async Task<IReadOnlyList<ShowcaseEvent>> GetEventsAsync(
        Guid organizationId, ShowcaseEventFilter filter, int limit, int offset, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(filter);
        IReadOnlyList<ShowcaseEvent> all = await LoadAsync(token);
        return all.Where(item => Matches(item, filter))
            .OrderBy(item => item.StartsOn is null)
            .ThenBy(item => item.StartsOn)
            .ThenBy(item => item.Title.ToLowerInvariant(), StringComparer.Ordinal)
            .ThenBy(item => item.Id)
            .Skip(offset)
            .Take(limit)
            .ToList();
    }

    public async Task<ShowcaseEventSummary> GetSummaryAsync(
        Guid organizationId, DateOnly today, int upcomingLimit, CancellationToken token)
    {
        IReadOnlyList<ShowcaseEvent> all = await LoadAsync(token);
        var byStatus = all.GroupBy(item => item.Status, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var upcoming = all
            .Where(item => item.ApplicationDeadline >= today && !ClosedStatuses.Contains(item.Status))
            .OrderBy(item => item.ApplicationDeadline)
            .ThenBy(item => item.Title.ToLowerInvariant(), StringComparer.Ordinal)
            .Take(upcomingLimit)
            .ToList();
        return new ShowcaseEventSummary(byStatus, upcoming);
    }

    public async Task<Result<ShowcaseEvent>> GetEventAsync(Guid organizationId, Guid eventId, CancellationToken token)
    {
        Result<TrackedEventModel> found = await tracking.GetAsync(Catalog, eventId, token);
        return found switch
        {
            Result<TrackedEventModel>.Success { Value: var tracked } when EventsServiceMapper.IsVisible(tracked.Event) =>
                Result<ShowcaseEvent>.Ok(EventsServiceMapper.ToShowcaseEvent(tracked)),
            Result<TrackedEventModel>.Failure failure => Result<ShowcaseEvent>.Fail(failure.Error),
            _ => Result<ShowcaseEvent>.Fail(DeskErrors.NotFound("The event was not found.")),
        };
    }

    /// <summary>Proposes the event to the catalog as a candidate and records this organization's tracking of it.</summary>
    public async Task<Result<ShowcaseEvent>> CreateEventAsync(
        Guid organizationId, string actor, ShowcaseEventInput input, CancellationToken token)
    {
        Result<EventModel> proposed = await catalogEvents.ProposeAsync(
            Catalog, new EventSpec { Facts = EventsServiceMapper.ToFacts(input) }, token);
        if (proposed is Result<EventModel>.Failure refused) return Result<ShowcaseEvent>.Fail(refused.Error);
        EventModel created = ((Result<EventModel>.Success)proposed).Value;
        Result<TrackingModel> tracked = await tracking.PutAsync(
            Catalog, created.Id, EventsServiceMapper.ToTracking(input, null), token);
        return tracked switch
        {
            Result<TrackingModel>.Success { Value: var own } => Result<ShowcaseEvent>.Ok(
                EventsServiceMapper.ToShowcaseEvent(new TrackedEventModel { Event = created, Tracking = own })),
            Result<TrackingModel>.Failure failure => Result<ShowcaseEvent>.Fail(failure.Error),
            _ => throw new InvalidOperationException("A result is a success or a failure."),
        };
    }

    /// <summary>
    /// Records stage, priority, notes, participation and cost at the revision Desk read; a change to the event's facts
    /// answers Gone, since facts change through Events' review.
    /// </summary>
    public async Task<Result<ShowcaseEvent>> UpdateEventAsync(
        Guid organizationId, Guid eventId, int expectedRevision, ShowcaseEventInput input, CancellationToken token)
    {
        Result<TrackedEventModel> found = await tracking.GetAsync(Catalog, eventId, token);
        if (found is Result<TrackedEventModel>.Failure missing) return Result<ShowcaseEvent>.Fail(missing.Error);
        TrackedEventModel current = ((Result<TrackedEventModel>.Success)found).Value;
        if (!EventsServiceMapper.IsVisible(current.Event))
            return Result<ShowcaseEvent>.Fail(DeskErrors.NotFound("The event was not found."));
        ShowcaseEvent shown = EventsServiceMapper.ToShowcaseEvent(current);
        if (shown.Revision != expectedRevision)
            return Result<ShowcaseEvent>.Fail(
                DeskErrors.Conflict("The event changed since it was loaded. Reload it before saving again."));
        if (!EventsServiceMapper.HasSameFacts(shown, input)) return Result<ShowcaseEvent>.Fail(FactsMoved());
        Result<TrackingModel> saved = await tracking.PutAsync(
            Catalog, eventId, EventsServiceMapper.ToTracking(input, EventsServiceMapper.TrackingRevision(expectedRevision)),
            token);
        return saved switch
        {
            Result<TrackingModel>.Success { Value: var own } => Result<ShowcaseEvent>.Ok(
                EventsServiceMapper.ToShowcaseEvent(current with { Tracking = own })),
            Result<TrackingModel>.Failure failure => Result<ShowcaseEvent>.Fail(failure.Error),
            _ => throw new InvalidOperationException("A result is a success or a failure."),
        };
    }

    /// <summary>Refuses: archiving an event is a reviewer's decision in Events.</summary>
    public Task<Result> DeleteEventAsync(
        Guid organizationId, Guid eventId, int expectedRevision, CancellationToken token)
    {
        return Task.FromResult(Result.Fail(FactsMoved()));
    }

    /// <summary>Reads every visible event of the catalog with this organization's tracking, following the cursor.</summary>
    private async Task<IReadOnlyList<ShowcaseEvent>> LoadAsync(CancellationToken token)
    {
        var all = new List<ShowcaseEvent>();
        string? cursor = null;
        for (var page = 0; page < MaximumPages; page++)
        {
            Result<TrackingPage> read = await tracking.ListAsync(Catalog, new TrackingQuery
            {
                TrackedOnly = false,
                IncludeUndated = true,
                Limit = EventsConstants.MaxPageSize,
                Cursor = cursor,
            }, token);
            if (read is Result<TrackingPage>.Failure failure) throw failure.Error.ToException();
            TrackingPage items = ((Result<TrackingPage>.Success)read).Value;
            all.AddRange(items.Items.Where(item => EventsServiceMapper.IsVisible(item.Event))
                .Select(EventsServiceMapper.ToShowcaseEvent));
            cursor = items.NextCursor;
            if (cursor is null) return all;
        }
        throw DeskErrors.Upstream("WoW2 Events answered more pages than Desk reads.").ToException();
    }

    /// <summary>Applies Desk's feed filter, the rules of its own SQL.</summary>
    private static bool Matches(ShowcaseEvent item, ShowcaseEventFilter filter)
    {
        if (filter.Query is { } query && !new[] { item.Title, item.Organizer, item.City, item.Venue, item.Country, item.Key }
                .Any(text => text?.Contains(query, StringComparison.OrdinalIgnoreCase) == true))
            return false;
        if (filter.Scope is { } scope && item.Scope != scope) return false;
        if (filter.Format is { } format && item.Format != format) return false;
        if (filter.Status is { } status && item.Status != status) return false;
        if (item.StartsOn is not { } start) return filter.IncludeUndated;
        DateOnly last = item.EndsOn ?? (item.DatePrecision is "month" or "estimated"
            ? new DateOnly(start.Year, start.Month, 1).AddMonths(1).AddDays(-1)
            : start);
        return (filter.From is not { } from || last >= from) && (filter.To is not { } to || start <= to);
    }

    private static AppError FactsMoved() => DeskErrors.Gone(
        "Event details moved to WoW2 Events; change them through its review. Stage, priority and notes stay editable here.");
}
