using WoW.Two.Sdk.Beta.Be.Events.Calendar;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Tracking;
using WoW.Two.Sdk.Beta.Be.Events.Workflows;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace OcharoEvents.Tests.Harness;

/// <summary>
/// Stands in for WoW2 Events behind the Events page's mirror (OE07): each broker call is recorded with what the mirror
/// passed (the catalog, the query, the spec) and answers the canned model a test set, so a test proves a route reaches
/// Events once, with the kit's filters and cursor unchanged. The mirror keeps no rule of its own to fake: Events' rules
/// are proven by its own conformance suites (<c>WoW2.Sdk.Beta.Be.Events.Testing</c>).
/// </summary>
internal sealed class RecordingEventsBrokers : IEventTrackingBroker, ICatalogEventsBroker, IEventWorkflowsBroker, IEventCalendarBroker
{
    public static readonly DateTimeOffset At = new(2026, 11, 19, 9, 0, 0, TimeSpan.Zero);

    /// <summary>Gets every call, in order: the broker method's name and its arguments after the catalog.</summary>
    public List<(string Method, string? Catalog, object? Argument)> Calls { get; } = [];

    /// <summary>Gets or sets the failure every call answers instead of its model; none by default.</summary>
    public AppError? Failure { get; set; }

    /// <summary>Gets or sets the page the tracking list answers.</summary>
    public TrackingPage Page { get; set; } = new([], null);

    /// <summary>Gets or sets the workflow the workflow read answers.</summary>
    public WorkflowModel Workflow { get; set; } = new()
    {
        Stages = [new WorkflowStageModel { Key = "candidate" }, new WorkflowStageModel { Key = "skipped", Class = StageClass.Lost }],
        DefaultStage = "candidate",
        Revision = 3,
    };

    /// <summary>Builds a published event of the catalog with a title in one language.</summary>
    public static EventModel Event(Guid id, string title, string language = "und") => new()
    {
        Id = id,
        Catalog = "ocharo-fashion",
        Facts = new EventFacts
        {
            Title = new Dictionary<string, string> { [language] = title },
            Kind = "runway",
            Place = new EventPlace { City = "Tashkent", Country = "UZ" },
            Starts = new EventWhen { Value = "2026-12-03", Precision = DatePrecision.Day },
        },
        Review = EventReviewState.Published,
        Revision = 1,
        CreatedAtUtc = At,
        UpdatedAtUtc = At,
    };

    /// <summary>Builds the organization's tracking of an event.</summary>
    public static TrackingModel Tracking(Guid eventId, string stage, int revision = 1) => new()
    {
        Id = Guid.NewGuid(),
        EventId = eventId,
        Stage = stage,
        Priority = TrackingPriority.Normal,
        Revision = revision,
        CreatedAtUtc = At,
        UpdatedAtUtc = At,
    };

    public Task<Result<TrackingPage>> ListAsync(string catalog, TrackingQuery query, CancellationToken cancellationToken = default) =>
        Answer(nameof(ListAsync), catalog, query, Page);

    public Task<Result<TrackedEventModel>> GetAsync(string catalog, Guid eventId, CancellationToken cancellationToken = default) =>
        Answer<TrackedEventModel>(nameof(IEventTrackingBroker.GetAsync), catalog, eventId,
            new TrackedEventModel { Event = Event(eventId, "Tashkent Fashion Week") });

    public Task<Result<TrackingModel>> PutAsync(string catalog, Guid eventId, TrackingSpec spec, CancellationToken cancellationToken = default) =>
        Answer(nameof(PutAsync), catalog, spec,
            Tracking(eventId, spec.Stage ?? Workflow.DefaultStage, (spec.ExpectedRevision ?? 0) + 1));

    public Task<Result<TrackingModel>> DeleteAsync(string catalog, Guid eventId, int expectedRevision, CancellationToken cancellationToken = default) =>
        Answer(nameof(DeleteAsync), catalog, expectedRevision, Tracking(eventId, "shortlisted", expectedRevision));

    public Task<Result<IReadOnlyList<TrackingHistoryEntryModel>>> GetHistoryAsync(
        string catalog, Guid eventId, CancellationToken cancellationToken = default) =>
        Answer<IReadOnlyList<TrackingHistoryEntryModel>>(nameof(GetHistoryAsync), catalog, eventId,
            [new TrackingHistoryEntryModel { ToStage = "shortlisted", AtUtc = At }]);

    public Task<Result<TrackingSummaryModel>> GetSummaryAsync(string catalog, DateOnly today, CancellationToken cancellationToken = default) =>
        GetSummaryAsync(catalog, new TrackingSummaryQuery { Today = today }, cancellationToken);

    public Task<Result<TrackingSummaryModel>> GetSummaryAsync(
        string catalog, TrackingSummaryQuery query, CancellationToken cancellationToken = default) =>
        Answer(nameof(GetSummaryAsync), catalog, query,
            new TrackingSummaryModel { ByStage = new Dictionary<string, int> { ["shortlisted"] = 2 }, Untracked = 64, UpcomingDeadlines = [] });

    public Task<Result<EventPage>> ListAsync(string catalog, EventQuery query, CancellationToken cancellationToken = default) =>
        Answer(nameof(ICatalogEventsBroker.ListAsync) + ":events", catalog, query, new EventPage([], null));

    Task<Result<EventModel>> ICatalogEventsBroker.GetAsync(string catalog, Guid id, CancellationToken cancellationToken) =>
        Answer("GetEventAsync", catalog, id, Event(id, "Tashkent Fashion Week"));

    public Task<Result<EventModel>> CreateAsync(string catalog, EventSpec spec, CancellationToken cancellationToken = default) =>
        Answer(nameof(CreateAsync), catalog, spec, Event(Guid.NewGuid(), "created"));

    public Task<Result<EventModel>> ProposeAsync(string catalog, EventSpec spec, CancellationToken cancellationToken = default) =>
        Answer(nameof(ProposeAsync), catalog, spec, Event(Guid.NewGuid(), "proposed") with { Facts = spec.Facts });

    public Task<Result<EventModel>> UpdateAsync(string catalog, Guid id, EventSpec spec, CancellationToken cancellationToken = default) =>
        Answer(nameof(UpdateAsync), catalog, spec, Event(id, "updated"));

    public Task<Result<EventModel>> ArchiveAsync(string catalog, Guid id, int expectedRevision, CancellationToken cancellationToken = default) =>
        Answer(nameof(ArchiveAsync), catalog, expectedRevision, Event(id, "archived"));

    Task<Result<WorkflowModel>> IEventWorkflowsBroker.GetAsync(CancellationToken cancellationToken) =>
        Answer("GetWorkflowAsync", null, null, Workflow);

    Task<Result<WorkflowModel>> IEventWorkflowsBroker.PutAsync(WorkflowSpec spec, CancellationToken cancellationToken) =>
        Answer("PutWorkflowAsync", null, spec,
            new WorkflowModel { Stages = spec.Stages, DefaultStage = spec.DefaultStage, Fields = spec.Fields, Revision = Workflow.Revision + 1 });

    public Task<Result<CalendarFile>> ExportAsync(string catalog, EventQuery query, CancellationToken cancellationToken = default) =>
        Answer(nameof(ExportAsync), catalog, query, new CalendarFile("ocharo-fashion.ics", "BEGIN:VCALENDAR\r\nEND:VCALENDAR\r\n"));

    /// <summary>Gets the calls of one broker method.</summary>
    public IReadOnlyList<(string Method, string? Catalog, object? Argument)> Of(string method) =>
        Calls.Where(call => call.Method == method).ToList();

    private Task<Result<T>> Answer<T>(string method, string? catalog, object? argument, T value) where T : notnull
    {
        Calls.Add((method, catalog, argument));
        return Task.FromResult(Failure is { } failure ? Result<T>.Fail(failure) : Result<T>.Ok(value));
    }
}
