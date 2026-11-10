using OcharoEvents.Application.Access.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoEvents.Application.Events.Commands;

/// <summary>Represents a command to delete a showcase event at an expected revision.</summary>
public sealed record EventDeleteCommand : ICommand<AppResult<Guid>>
{
    /// <summary>Gets the caller's resolved organization membership.</summary>
    public required OrganizationAccess Access { get; init; }

    /// <summary>Gets the event to delete.</summary>
    public required Guid EventId { get; init; }

    /// <summary>Gets the revision the operator saw.</summary>
    public required int ExpectedRevision { get; init; }
}
