using OcharoEvents.Application.Access.Models;
using OcharoEvents.Application.Events.Models;
using OcharoEvents.Domain.Events.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoEvents.Application.Events.Commands;

/// <summary>Represents a command to record where Ocharo stands with an event at an expected tracking revision, whatever has
/// happened to the event's facts since the operator read it.</summary>
public sealed record EventTrackingUpdateCommand : ICommand<AppResult<ShowcaseEvent>>
{
    /// <summary>Gets the caller's resolved organization membership.</summary>
    public required OrganizationAccess Access { get; init; }

    /// <summary>Gets the event whose tracking changes.</summary>
    public required Guid EventId { get; init; }

    /// <summary>Gets the revision the operator edited.</summary>
    public required int ExpectedRevision { get; init; }

    /// <summary>Gets the fields as the operator sent them.</summary>
    public required TrackingFields Fields { get; init; }
}
