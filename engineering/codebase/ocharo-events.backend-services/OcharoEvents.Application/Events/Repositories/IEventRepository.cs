using OcharoEvents.Application.Events.Models;
using OcharoEvents.Domain.Events.Models;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace OcharoEvents.Application.Events.Repositories;

/// <summary>Persists organization-scoped showcase events.</summary>
public interface IEventRepository
{
    Task<IReadOnlyList<ShowcaseEvent>> GetEventsAsync(
        Guid organizationId,
        ShowcaseEventFilter filter,
        int limit,
        int offset,
        CancellationToken token);

    /// <summary>
    /// Counts only the statuses in use; the nearest deadlines on or after today, of events Ocharo skipped or rejected or that were cancelled excluded.
    /// </summary>
    Task<ShowcaseEventSummary> GetSummaryAsync(
        Guid organizationId,
        DateOnly today,
        int upcomingLimit,
        CancellationToken token);

    /// <summary>Reads one event; not found when the organization lacks it.</summary>
    Task<Result<ShowcaseEvent>> GetEventAsync(Guid organizationId, Guid eventId, CancellationToken token);

    /// <summary>Adds an event the person proposes.</summary>
    Task<Result<ShowcaseEvent>> CreateEventAsync(
        Guid organizationId,
        string actor,
        ShowcaseEventInput input,
        CancellationToken token);

    /// <summary>
    /// Changes an event at its expected revision; not found, or a conflict when it changed since. When the event's own
    /// revision is given and differs from the current one the facts moved since the page read them, a conflict; otherwise a
    /// change to the facts is refused as gone.
    /// </summary>
    Task<Result<ShowcaseEvent>> UpdateEventAsync(
        Guid organizationId,
        Guid eventId,
        int expectedRevision,
        int? expectedEventRevision,
        ShowcaseEventInput input,
        CancellationToken token);

    /// <summary>
    /// Records where the organization stands with an event at its expected tracking revision, whatever has happened to the
    /// event's facts since; not found, or a conflict when the tracking changed since.
    /// </summary>
    Task<Result<ShowcaseEvent>> UpdateTrackingAsync(
        Guid organizationId,
        Guid eventId,
        int expectedRevision,
        ShowcaseTrackingInput input,
        CancellationToken token);

    /// <summary>Removes an event at its expected revision; not found, or a conflict when it changed since.</summary>
    Task<Result> DeleteEventAsync(Guid organizationId, Guid eventId, int expectedRevision, CancellationToken token);
}
