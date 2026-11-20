using WoW.Two.Sdk.Beta.Be.Events.Calendar;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Intake;
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
internal sealed class RecordingEventsBrokers : IEventTrackingBroker, ICatalogEventsBroker, IEventWorkflowsBroker, IEventCalendarBroker,
    IEventIntakeBroker
{
    public static readonly DateTimeOffset At = new(2026, 11, 19, 9, 0, 0, TimeSpan.Zero);

    /// <summary>Gets every call, in order: the broker method's name and its arguments after the catalog.</summary>
    public List<(string Method, string? Catalog, object? Argument)> Calls { get; } = [];

    /// <summary>Gets or sets the failure every call answers instead of its model; none by default.</summary>
    public AppError? Failure { get; set; }

    /// <summary>Gets or sets the page the tracking list answers.</summary>
    public TrackingPage Page { get; set; } = new([], null);

    /// <summary>Gets or sets the tracked events the facets read counts: the fake's own data, over which the counts are
    /// computed for the query the mirror passed, each facet without its own selection; empty by default.</summary>
    public List<TrackedEventModel> Tracked { get; set; } = [];

    /// <summary>Gets or sets the workflow the workflow read answers.</summary>
    public WorkflowModel Workflow { get; set; } = new()
    {
        Stages = [new WorkflowStageModel { Key = "candidate" }, new WorkflowStageModel { Key = "skipped", Class = StageClass.Lost }],
        DefaultStage = "candidate",
        Revision = 3,
    };

    /// <summary>Gets or sets the freshness the intake read answers: a pull a few hours ago, no source failing.</summary>
    public IntakeFreshnessModel Freshness { get; set; } = new()
    {
        LastReceivedAtUtc = At.AddHours(-2),
        NewestObservedAtUtc = At.AddHours(-3),
        FailingSources = 0,
    };

    /// <summary>Gets or sets the images the single-event read answers with, as SCFD's copies reach Events: each the source
    /// <c>url</c> and, for a copy, its Files <c>fileRef</c>.</summary>
    public IReadOnlyList<EventImage> Images { get; set; } = [];

    /// <summary>Builds a published event of the catalog with a title in one language.</summary>
    public static EventModel Event(Guid id, string title, string language = "und", IReadOnlyList<EventImage>? images = null) => new()
    {
        Id = id,
        Catalog = "ocharo-fashion",
        Facts = new EventFacts
        {
            Title = new Dictionary<string, string> { [language] = title },
            Kind = "runway",
            Place = new EventPlace { City = "Tashkent", Country = "UZ" },
            Starts = new EventWhen { Value = "2026-12-03", Precision = DatePrecision.Day },
            Images = images ?? [],
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

    /// <summary>Counts <see cref="Tracked"/> as Events counts a tracking list's chips: the total honours the whole query,
    /// and the stage, kind, city and country facets each leave out their own selection. Priorities, sources and the open
    /// deadline are Events' own rules, proven by its conformance suites, and answer empty here.</summary>
    public Task<Result<TrackingFacetsModel>> GetFacetsAsync(string catalog, TrackingQuery query, CancellationToken cancellationToken = default)
    {
        IEnumerable<TrackedEventModel> Without(Facet facet) => Tracked.Where(item => Matches(item, query, facet));
        static List<TrackingFacetCount> Counts(IEnumerable<string?> values) =>
            values.OfType<string>().GroupBy(value => value).Select(group => new TrackingFacetCount(group.Key, group.Count(), null))
                .OrderByDescending(count => count.Count).ToList();

        var model = new TrackingFacetsModel
        {
            Total = Without(Facet.None).Count(),
            Countries = Counts(Without(Facet.Country).Select(item => item.Event.Facts.Place?.Country)),
            Cities = Counts(Without(Facet.City).Select(item => item.Event.Facts.Place?.City?.ToLowerInvariant())),
            Kinds = Counts(Without(Facet.Kind).Select(item => item.Event.Facts.Kind)),
            Stages = Workflow.Stages.Select(stage => new TrackingFacetCount(
                stage.Key, Without(Facet.Stage).Count(item => StageOf(item) == stage.Key), null)).ToList(),
            Priorities = [],
            Sources = [],
            OpenDeadline = new TrackingDeadlineFacet(query.DeadlineBefore ?? DateOnly.FromDateTime(At.UtcDateTime), 0),
        };
        return Answer(nameof(GetFacetsAsync), catalog, query, model);
    }

    public Task<Result<TrackingSummaryModel>> GetSummaryAsync(string catalog, DateOnly today, CancellationToken cancellationToken = default) =>
        GetSummaryAsync(catalog, new TrackingSummaryQuery { Today = today }, cancellationToken);

    public Task<Result<TrackingSummaryModel>> GetSummaryAsync(
        string catalog, TrackingSummaryQuery query, CancellationToken cancellationToken = default) =>
        Answer(nameof(GetSummaryAsync), catalog, query,
            new TrackingSummaryModel { ByStage = new Dictionary<string, int> { ["shortlisted"] = 2 }, Untracked = 64, UpcomingDeadlines = [] });

    public Task<Result<EventPage>> ListAsync(string catalog, EventQuery query, CancellationToken cancellationToken = default) =>
        Answer(nameof(ICatalogEventsBroker.ListAsync) + ":events", catalog, query, new EventPage([], null));

    Task<Result<EventModel>> ICatalogEventsBroker.GetAsync(string catalog, Guid id, CancellationToken cancellationToken) =>
        Answer("GetEventAsync", catalog, id, Event(id, "Tashkent Fashion Week", images: Images));

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

    public Task<Result<IntakeFreshnessModel>> GetFreshnessAsync(string catalog, CancellationToken cancellationToken = default) =>
        Answer(nameof(GetFreshnessAsync), catalog, null, Freshness);

    /// <summary>The status and the poll are operator calls no Events page route reaches; a call here is a mistake.</summary>
    public Task<Result<IntakeStatusModel>> GetStatusAsync(CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("The Events page's mirror never reads the intake status.");

    /// <inheritdoc cref="GetStatusAsync" />
    public Task<Result<IntakeStatusModel>> PollAsync(CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("The Events page's mirror never polls the intake.");

    /// <summary>Gets the calls of one broker method.</summary>
    public IReadOnlyList<(string Method, string? Catalog, object? Argument)> Of(string method) =>
        Calls.Where(call => call.Method == method).ToList();

    private string StageOf(TrackedEventModel item) => item.Tracking?.Stage ?? Workflow.DefaultStage;

    /// <summary>Says whether an event matches the query's selections, leaving out the one of the facet being counted.</summary>
    private bool Matches(TrackedEventModel item, TrackingQuery query, Facet skipped)
    {
        EventFacts facts = item.Event.Facts;
        string stage = StageOf(item);
        bool Selected(IEnumerable<string?> selection, string? value, bool ignoreCase = false) =>
            !selection.OfType<string>().Any()
            || selection.OfType<string>().Contains(value, ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

        return (skipped == Facet.Stage || (Selected([query.Stage, ..query.Stages], stage) && !query.ExcludeStages.Contains(stage)))
            && (skipped == Facet.Kind || Selected([query.Kind, ..query.Kinds], facts.Kind))
            && (skipped == Facet.City || Selected([query.City, ..query.Cities], facts.Place?.City, ignoreCase: true))
            && (skipped == Facet.Country
                || (Selected([query.Country, ..query.Countries], facts.Place?.Country)
                    && !query.ExcludeCountries.Contains(facts.Place?.Country ?? "")))
            && (!query.TrackedOnly || item.Tracking is not null);
    }

    private enum Facet { None, Stage, Kind, City, Country }

    private Task<Result<T>> Answer<T>(string method, string? catalog, object? argument, T value) where T : notnull
    {
        Calls.Add((method, catalog, argument));
        return Task.FromResult(Failure is { } failure ? Result<T>.Fail(failure) : Result<T>.Ok(value));
    }
}
