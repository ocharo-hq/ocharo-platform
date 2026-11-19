using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Beta.Be.Events.Calendar;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Tracking;
using WoW.Two.Sdk.Beta.Be.Events.Workflows;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace OcharoEvents.Infrastructure.Events.Services;

/// <summary>
/// Serves the Events page from WoW2 Events as the <c>fe-events</c> kit reads it (OE07): the organization's tracking of the
/// page's catalog, one event, a member's proposal, the organization's workflow and the catalog's calendar file, each
/// passed through to Events' brokers as the kit sent it, so a filter, a cursor or a new fact reaches Events with no
/// mapping here. Every call acts for the signed-in person through the brokers' token exchange, so Events keeps the
/// tracking to the session's organization and checks its scopes and permissions again.
/// </summary>
/// <remarks>Only the configured catalog is served: another slug answers 404 before reaching Events, and an unconfigured
/// Events answers 503, as the digest routes do.</remarks>
public sealed class EventsApiService(
    IEventTrackingBroker tracking,
    ICatalogEventsBroker catalogEvents,
    IEventWorkflowsBroker workflows,
    IEventCalendarBroker calendar,
    IOptions<EventsServiceOptions> options)
{
    /// <summary>Lists the catalog's published events with the organization's tracking: one Events call, its filters and
    /// cursor as given.</summary>
    public Task<Result<TrackingPage>> ListTrackingAsync(string catalog, TrackingQuery query, CancellationToken token) =>
        Refused<TrackingPage>(catalog) ?? tracking.ListAsync(Catalog, query, token);

    /// <summary>Counts the organization's tracking per stage and lists the nearest open deadlines.</summary>
    public Task<Result<TrackingSummaryModel>> GetTrackingSummaryAsync(
        string catalog, TrackingSummaryQuery query, CancellationToken token) =>
        Refused<TrackingSummaryModel>(catalog) ?? tracking.GetSummaryAsync(Catalog, query, token);

    /// <summary>Gets one event with the organization's tracking.</summary>
    public Task<Result<TrackedEventModel>> GetTrackingAsync(string catalog, Guid eventId, CancellationToken token) =>
        Refused<TrackedEventModel>(catalog) ?? tracking.GetAsync(Catalog, eventId, token);

    /// <summary>Starts or changes the organization's tracking of an event at the revision the page read.</summary>
    public Task<Result<TrackingModel>> PutTrackingAsync(
        string catalog, Guid eventId, TrackingSpec spec, CancellationToken token) =>
        Refused<TrackingModel>(catalog) ?? tracking.PutAsync(Catalog, eventId, spec, token);

    /// <summary>Stops tracking an event at the revision the page read.</summary>
    public Task<Result<TrackingModel>> DeleteTrackingAsync(
        string catalog, Guid eventId, int expectedRevision, CancellationToken token) =>
        Refused<TrackingModel>(catalog) ?? tracking.DeleteAsync(Catalog, eventId, expectedRevision, token);

    /// <summary>Lists the stage changes of the organization's tracking of an event, oldest first.</summary>
    public Task<Result<IReadOnlyList<TrackingHistoryEntryModel>>> GetTrackingHistoryAsync(
        string catalog, Guid eventId, CancellationToken token) =>
        Refused<IReadOnlyList<TrackingHistoryEntryModel>>(catalog) ?? tracking.GetHistoryAsync(Catalog, eventId, token);

    /// <summary>Gets one event of the catalog.</summary>
    public Task<Result<EventModel>> GetEventAsync(string catalog, Guid id, CancellationToken token) =>
        Refused<EventModel>(catalog) ?? catalogEvents.GetAsync(Catalog, id, token);

    /// <summary>Proposes an event; Events publishes a member's proposal in the organization's private catalog at once.</summary>
    public Task<Result<EventModel>> ProposeEventAsync(string catalog, EventSpec spec, CancellationToken token) =>
        Refused<EventModel>(catalog) ?? catalogEvents.ProposeAsync(Catalog, spec, token);

    /// <summary>Gets the organization's workflow: its stages, default stage and custom fields.</summary>
    public Task<Result<WorkflowModel>> GetWorkflowAsync(CancellationToken token) =>
        Refused<WorkflowModel>(null) ?? workflows.GetAsync(token);

    /// <summary>Writes the organization's workflow at the revision the page read.</summary>
    public Task<Result<WorkflowModel>> PutWorkflowAsync(WorkflowSpec spec, CancellationToken token) =>
        Refused<WorkflowModel>(null) ?? workflows.PutAsync(spec, token);

    /// <summary>Exports the catalog's published events the query selects as an iCalendar file.</summary>
    public Task<Result<CalendarFile>> ExportCalendarAsync(string catalog, EventQuery query, CancellationToken token) =>
        Refused<CalendarFile>(catalog) ?? calendar.ExportAsync(Catalog, query, token);

    private string Catalog => options.Value.Catalog;

    private Task<Result<T>>? Refused<T>(string? catalog) where T : notnull =>
        EventsCatalogGuard.Refusal(options.Value, catalog) is { } refusal ? Fail<T>(refusal) : null;

    private static Task<Result<T>> Fail<T>(AppError error) where T : notnull => Task.FromResult(Result<T>.Fail(error));
}
