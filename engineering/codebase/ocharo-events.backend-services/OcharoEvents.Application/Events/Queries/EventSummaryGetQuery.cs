using OcharoEvents.Application.Access.Models;
using OcharoEvents.Domain.Events.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoEvents.Application.Events.Queries;

/// <summary>Represents a query to count the organization's events per status and list the nearest deadlines.</summary>
public sealed record EventSummaryGetQuery : IQuery<AppResult<ShowcaseEventSummary>>
{
    /// <summary>Gets the caller's resolved organization membership.</summary>
    public required OrganizationAccess Access { get; init; }

    /// <summary>Gets the ISO date that counts as today; the UTC date when absent.</summary>
    public string? Today { get; init; }
}
