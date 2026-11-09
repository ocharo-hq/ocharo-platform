using BrandWorkspace.Application.Access.Models;
using BrandWorkspace.Domain.Sourcing.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Application.Sourcing.Commands;

/// <summary>Represents a command to replace a route at an expected revision.</summary>
public sealed record RouteUpdateCommand : ICommand<AppResult<ContactRoute>>
{
    /// <summary>Gets the caller's resolved organization membership.</summary>
    public required OrganizationAccess Access { get; init; }

    /// <summary>Gets the route to replace.</summary>
    public required Guid RouteId { get; init; }

    /// <summary>Gets the revision the operator edited.</summary>
    public required int ExpectedRevision { get; init; }

    /// <summary>Gets the channel.</summary>
    public string? Channel { get; init; }

    /// <summary>Gets the destination.</summary>
    public string? Destination { get; init; }

    /// <summary>Gets the label.</summary>
    public string? Label { get; init; }

    /// <summary>Gets the link the route was found at.</summary>
    public string? SourceUrl { get; init; }

    /// <summary>Gets whether the route was verified.</summary>
    public required bool Verified { get; init; }

    /// <summary>Gets whether the route is the supplier's primary one.</summary>
    public required bool IsPrimary { get; init; }

    /// <summary>Gets the person the route reaches, if any.</summary>
    public Guid? PersonId { get; init; }

    /// <summary>Gets the notes.</summary>
    public string? Notes { get; init; }
}
