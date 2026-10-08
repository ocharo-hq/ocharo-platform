using BrandWorkspace.Application.Access;
using BrandWorkspace.Application.Events.Models;
using BrandWorkspace.Application.Events.Repositories;
using BrandWorkspace.Application.Preparation.Models;
using BrandWorkspace.Domain.Events.Models;

namespace BrandWorkspace.Infrastructure.Events.Services;

/// <summary>Enforces membership roles and validation for the showcase events feed.</summary>
public sealed class EventService(IEventRepository repository, EventValidationService validation, TimeProvider time)
{
    public const int UpcomingDeadlineCount = 5;

    public Task<IReadOnlyList<ShowcaseEvent>> GetEventsAsync(
        OrganizationAccess access, string? query, string? scope, string? format, string? status, string? from, string? to,
        bool includeUndated, int limit, int offset, CancellationToken token)
    {
        validation.Page(limit, offset);
        ShowcaseEventFilter filter = validation.Filter(query, scope, format, status, from, to, includeUndated);
        return repository.GetEventsAsync(access.OrganizationId, filter, limit, offset, token);
    }

    /// <summary>Counts every status, zero included, and lists the nearest open application deadlines.</summary>
    public async Task<ShowcaseEventSummary> GetSummaryAsync(OrganizationAccess access, string? today, CancellationToken token)
    {
        DateOnly date = validation.Today(today) ?? DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
        ShowcaseEventSummary summary = await repository.GetSummaryAsync(access.OrganizationId, date, UpcomingDeadlineCount, token);
        var byStatus = EventValidationService.Statuses.ToDictionary(
            status => status, status => summary.ByStatus.GetValueOrDefault(status), StringComparer.Ordinal);
        return summary with { ByStatus = byStatus };
    }

    public Task<ShowcaseEvent> GetEventAsync(OrganizationAccess access, Guid eventId, CancellationToken token) =>
        repository.GetEventAsync(access.OrganizationId, eventId, token);

    public Task<ShowcaseEvent> CreateEventAsync(OrganizationAccess access, ShowcaseEventInput input, CancellationToken token)
    {
        RequireEditor(access);
        return repository.CreateEventAsync(access.OrganizationId, access.UserId, null, input, token);
    }

    public Task<ShowcaseEvent> UpdateEventAsync(
        OrganizationAccess access, Guid eventId, int expectedRevision, ShowcaseEventInput input, CancellationToken token)
    {
        RequireEditor(access);
        validation.Revision(expectedRevision);
        return repository.UpdateEventAsync(access.OrganizationId, eventId, expectedRevision, input, token);
    }

    public Task DeleteEventAsync(OrganizationAccess access, Guid eventId, int expectedRevision, CancellationToken token)
    {
        RequireEditor(access);
        validation.Revision(expectedRevision);
        return repository.DeleteEventAsync(access.OrganizationId, eventId, expectedRevision, token);
    }

    private static void RequireEditor(OrganizationAccess access)
    {
        if (!access.CanEdit) throw new PreparationException("forbidden", "An editor or owner role is required.");
    }
}
