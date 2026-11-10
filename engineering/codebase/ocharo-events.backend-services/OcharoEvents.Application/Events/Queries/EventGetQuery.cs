using OcharoEvents.Application.Access.Models;
using OcharoEvents.Domain.Events.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoEvents.Application.Events.Queries;

/// <summary>Represents a query to get one showcase event.</summary>
public sealed record EventGetQuery : IQuery<AppResult<ShowcaseEvent>>
{
    /// <summary>Gets the caller's resolved organization membership.</summary>
    public required OrganizationAccess Access { get; init; }

    /// <summary>Gets the event to read.</summary>
    public required Guid EventId { get; init; }
}
