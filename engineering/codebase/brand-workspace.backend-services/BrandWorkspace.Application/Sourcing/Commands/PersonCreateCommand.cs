using BrandWorkspace.Application.Access.Models;
using BrandWorkspace.Domain.Sourcing.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Application.Sourcing.Commands;

/// <summary>Represents a command to record a person who speaks for a supplier.</summary>
public sealed record PersonCreateCommand : ICommand<AppResult<SupplierPerson>>
{
    /// <summary>Gets the caller's resolved organization membership.</summary>
    public required OrganizationAccess Access { get; init; }

    /// <summary>Gets the supplier the person speaks for.</summary>
    public required Guid SupplierId { get; init; }

    /// <summary>Gets the name.</summary>
    public string? Name { get; init; }

    /// <summary>Gets the role.</summary>
    public string? Role { get; init; }

    /// <summary>Gets the notes.</summary>
    public string? Notes { get; init; }
}
