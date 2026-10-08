using BrandWorkspace.Application.Events.Models;
using BrandWorkspace.Domain.Events.Models;

namespace BrandWorkspace.Application.Events.Repositories;

/// <summary>Persists organization-scoped showcase events.</summary>
public interface IEventRepository
{
    Task<IReadOnlyList<ShowcaseEvent>> GetEventsAsync(
        Guid organizationId, ShowcaseEventFilter filter, int limit, int offset, CancellationToken token);
    /// <summary>Counts only the statuses in use; the nearest deadlines on or after today, skipped and rejected excluded.</summary>
    Task<ShowcaseEventSummary> GetSummaryAsync(Guid organizationId, DateOnly today, int upcomingLimit, CancellationToken token);
    Task<ShowcaseEvent> GetEventAsync(Guid organizationId, Guid eventId, CancellationToken token);
    Task<ShowcaseEvent?> FindEventByKeyAsync(Guid organizationId, string key, CancellationToken token);
    Task<ShowcaseEvent> CreateEventAsync(
        Guid organizationId, string actor, string? key, ShowcaseEventInput input, CancellationToken token);
    Task<ShowcaseEvent> UpdateEventAsync(
        Guid organizationId, Guid eventId, int expectedRevision, ShowcaseEventInput input, CancellationToken token);
    Task DeleteEventAsync(Guid organizationId, Guid eventId, int expectedRevision, CancellationToken token);
}
