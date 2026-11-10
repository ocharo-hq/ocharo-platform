using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Tracking;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace OcharoEvents.Tests.Unit;

/// <summary>
/// An in-memory WoW2 Events catalog with one organization's tracking: the two brokers the events repository calls,
/// with the service's revision rules; every other member is unused and throws.
/// </summary>
public sealed class FakeEventsService : ICatalogEventsBroker, IEventTrackingBroker
{
    private static readonly DateTimeOffset At = new(2026, 11, 10, 8, 0, 0, TimeSpan.Zero);
    private readonly Dictionary<Guid, EventModel> events = [];
    private readonly Dictionary<Guid, TrackingModel> trackings = [];

    /// <summary>Gets the tracking specs the repository sent, in order.</summary>
    public List<TrackingSpec> TrackingWrites { get; } = [];

    /// <summary>Gets or sets the page size the fake answers with, so tests follow the cursor.</summary>
    public int PageSize { get; set; } = 200;

    public EventModel Add(EventFacts facts, EventReviewState review = EventReviewState.Published)
    {
        var item = new EventModel
        {
            Id = Guid.NewGuid(), Catalog = "ocharo-fashion", Facts = facts, Review = review, Revision = 1,
            CreatedAtUtc = At, UpdatedAtUtc = At,
        };
        events[item.Id] = item;
        return item;
    }

    public TrackingModel Track(Guid eventId, string stage, TrackingPriority priority = TrackingPriority.Normal, int revision = 1)
    {
        var tracking = new TrackingModel
        {
            Id = Guid.NewGuid(), EventId = eventId, Stage = stage, Priority = priority, Revision = revision,
            CreatedAtUtc = At, UpdatedAtUtc = At,
        };
        trackings[eventId] = tracking;
        return tracking;
    }

    public EventModel Event(Guid id) => events[id];

    public Task<Result<TrackingPage>> ListAsync(string catalog, TrackingQuery query, CancellationToken cancellationToken = default)
    {
        int start = query.Cursor is { } cursor ? int.Parse(cursor, System.Globalization.CultureInfo.InvariantCulture) : 0;
        var all = events.Values.OrderBy(item => item.Id).ToList();
        var page = all.Skip(start).Take(PageSize)
            .Select(item => new TrackedEventModel { Event = item, Tracking = trackings.GetValueOrDefault(item.Id) })
            .ToList();
        string? next = start + PageSize < all.Count ? (start + PageSize).ToString(System.Globalization.CultureInfo.InvariantCulture) : null;
        return Task.FromResult(Result<TrackingPage>.Ok(new TrackingPage(page, next)));
    }

    Task<Result<TrackedEventModel>> IEventTrackingBroker.GetAsync(string catalog, Guid eventId, CancellationToken cancellationToken) =>
        Task.FromResult(events.TryGetValue(eventId, out var item)
            ? Result<TrackedEventModel>.Ok(new TrackedEventModel { Event = item, Tracking = trackings.GetValueOrDefault(eventId) })
            : Result<TrackedEventModel>.Fail(AppError.Of(AppErrorType.NotFound, "The event was not found.", null, "", "", 0)));

    public Task<Result<TrackingModel>> PutAsync(string catalog, Guid eventId, TrackingSpec spec, CancellationToken cancellationToken = default)
    {
        TrackingWrites.Add(spec);
        trackings.TryGetValue(eventId, out var current);
        if (current?.Revision != spec.ExpectedRevision)
            return Task.FromResult(Result<TrackingModel>.Fail(AppError.Of(AppErrorType.Conflict, "Stale tracking.", null, "", "", 0)));
        var saved = new TrackingModel
        {
            Id = current?.Id ?? Guid.NewGuid(), EventId = eventId, Stage = spec.Stage!, Priority = spec.Priority,
            Notes = spec.Notes, Fields = spec.Fields, Revision = (current?.Revision ?? 0) + 1,
            CreatedAtUtc = current?.CreatedAtUtc ?? At, UpdatedAtUtc = At.AddMinutes(1),
        };
        trackings[eventId] = saved;
        return Task.FromResult(Result<TrackingModel>.Ok(saved));
    }

    public Task<Result<EventModel>> ProposeAsync(string catalog, EventSpec spec, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result<EventModel>.Ok(Add(spec.Facts, EventReviewState.Candidate)));

    public Task<Result<EventPage>> ListAsync(string catalog, EventQuery query, CancellationToken cancellationToken = default) => throw Unused();
    Task<Result<EventModel>> ICatalogEventsBroker.GetAsync(string catalog, Guid id, CancellationToken cancellationToken) => throw Unused();
    public Task<Result<EventModel>> CreateAsync(string catalog, EventSpec spec, CancellationToken cancellationToken = default) => throw Unused();
    public Task<Result<EventModel>> UpdateAsync(string catalog, Guid id, EventSpec spec, CancellationToken cancellationToken = default) => throw Unused();
    public Task<Result<EventModel>> ArchiveAsync(string catalog, Guid id, int expectedRevision, CancellationToken cancellationToken = default) => throw Unused();
    public Task<Result<TrackingModel>> DeleteAsync(string catalog, Guid eventId, int expectedRevision, CancellationToken cancellationToken = default) => throw Unused();
    public Task<Result<IReadOnlyList<TrackingHistoryEntryModel>>> GetHistoryAsync(string catalog, Guid eventId, CancellationToken cancellationToken = default) => throw Unused();
    public Task<Result<TrackingSummaryModel>> GetSummaryAsync(string catalog, DateOnly today, CancellationToken cancellationToken = default) => throw Unused();

    private static NotSupportedException Unused() => new("The events repository never calls this member.");
}
