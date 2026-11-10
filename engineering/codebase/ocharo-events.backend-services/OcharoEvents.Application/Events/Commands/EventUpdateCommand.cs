using OcharoEvents.Application.Access.Models;
using OcharoEvents.Application.Events.Models;
using OcharoEvents.Domain.Events.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoEvents.Application.Events.Commands;

/// <summary>Represents a command to replace a showcase event at an expected revision.</summary>
public sealed record EventUpdateCommand : ICommand<AppResult<ShowcaseEvent>>
{
    /// <summary>Gets the caller's resolved organization membership.</summary>
    public required OrganizationAccess Access { get; init; }

    /// <summary>Gets the event to replace.</summary>
    public required Guid EventId { get; init; }

    /// <summary>Gets the revision the operator edited.</summary>
    public required int ExpectedRevision { get; init; }

    /// <summary>Gets the fields as the operator sent them.</summary>
    public required EventFields Fields { get; init; }
}
