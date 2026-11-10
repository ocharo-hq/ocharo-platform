using BrandWorkspace.Application.Events.Models;
using BrandWorkspace.Domain.Events.Models;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace BrandWorkspace.Application.Events.Repositories;

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
    /// Counts only the statuses in use; the nearest deadlines on or after today, skipped and rejected excluded.
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

    /// <summary>Changes an event at its expected revision; not found, or a conflict when it changed since.</summary>
    Task<Result<ShowcaseEvent>> UpdateEventAsync(
        Guid organizationId,
        Guid eventId,
        int expectedRevision,
        ShowcaseEventInput input,
        CancellationToken token);

    /// <summary>Removes an event at its expected revision; not found, or a conflict when it changed since.</summary>
    Task<Result> DeleteEventAsync(Guid organizationId, Guid eventId, int expectedRevision, CancellationToken token);
}
