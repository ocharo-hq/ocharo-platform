using BrandWorkspace.Application.Access.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Application.Sourcing.Commands;

/// <summary>Represents a command to delete a person at an expected revision.</summary>
public sealed record PersonDeleteCommand : ICommand<AppResult<Guid>>
{
    /// <summary>Gets the caller's resolved organization membership.</summary>
    public required OrganizationAccess Access { get; init; }

    /// <summary>Gets the person to delete.</summary>
    public required Guid PersonId { get; init; }

    /// <summary>Gets the revision the operator saw.</summary>
    public required int ExpectedRevision { get; init; }
}
