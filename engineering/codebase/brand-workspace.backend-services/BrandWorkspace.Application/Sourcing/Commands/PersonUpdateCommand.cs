using BrandWorkspace.Application.Access.Models;
using BrandWorkspace.Domain.Sourcing.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Application.Sourcing.Commands;

/// <summary>Represents a command to replace a person at an expected revision.</summary>
public sealed record PersonUpdateCommand : ICommand<AppResult<SupplierPerson>>
{
    /// <summary>Gets the caller's resolved organization membership.</summary>
    public required OrganizationAccess Access { get; init; }

    /// <summary>Gets the person to replace.</summary>
    public required Guid PersonId { get; init; }

    /// <summary>Gets the revision the operator edited.</summary>
    public required int ExpectedRevision { get; init; }

    /// <summary>Gets the name.</summary>
    public string? Name { get; init; }

    /// <summary>Gets the role.</summary>
    public string? Role { get; init; }

    /// <summary>Gets the notes.</summary>
    public string? Notes { get; init; }
}
