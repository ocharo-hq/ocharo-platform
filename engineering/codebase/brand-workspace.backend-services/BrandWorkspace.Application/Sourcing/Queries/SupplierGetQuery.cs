using BrandWorkspace.Application.Access.Models;
using BrandWorkspace.Domain.Sourcing.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Application.Sourcing.Queries;

/// <summary>Represents a query to get one supplier with its people and routes.</summary>
public sealed record SupplierGetQuery : IQuery<AppResult<Supplier>>
{
    /// <summary>Gets the caller's resolved organization membership.</summary>
    public required OrganizationAccess Access { get; init; }

    /// <summary>Gets the supplier to read.</summary>
    public required Guid SupplierId { get; init; }
}
