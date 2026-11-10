using BrandWorkspace.Application.Events.Models;

namespace BrandWorkspace.Application.Events.Repositories;

/// <summary>
/// Reads Desk's own <c>events.showcase_events</c> table, the rollback copy once WoW2 Events serves Desk's page
/// (events-adoption.md #7); the export to Events reads through it whichever store serves the page.
/// </summary>
public interface IEventRecordsReader
{
    /// <summary>Reads every stored event of the organization with its author, oldest first.</summary>
    Task<IReadOnlyList<ShowcaseEventRecord>> GetEventRecordsAsync(Guid organizationId, CancellationToken token);
}
