using BrandWorkspace.Application.Access.Models;
using BrandWorkspace.Domain.Sourcing.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Application.Sourcing.Commands;

/// <summary>Represents a command to replace a supplier at an expected revision.</summary>
public sealed record SupplierUpdateCommand : ICommand<AppResult<Supplier>>
{
    /// <summary>Gets the caller's resolved organization membership.</summary>
    public required OrganizationAccess Access { get; init; }

    /// <summary>Gets the supplier to replace.</summary>
    public required Guid SupplierId { get; init; }

    /// <summary>Gets the revision the operator edited.</summary>
    public required int ExpectedRevision { get; init; }

    /// <summary>Gets the name.</summary>
    public string? Name { get; init; }

    /// <summary>Gets the outreach stage.</summary>
    public string? Stage { get; init; }

    /// <summary>Gets the country code.</summary>
    public string? Country { get; init; }

    /// <summary>Gets the city.</summary>
    public string? City { get; init; }

    /// <summary>Gets the website.</summary>
    public string? Website { get; init; }

    /// <summary>Gets the notes.</summary>
    public string? Notes { get; init; }
}
