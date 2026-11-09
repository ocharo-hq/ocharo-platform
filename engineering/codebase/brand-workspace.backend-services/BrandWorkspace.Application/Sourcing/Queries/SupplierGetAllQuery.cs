using BrandWorkspace.Application.Access.Models;
using BrandWorkspace.Domain.Sourcing.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Application.Sourcing.Queries;

/// <summary>Represents a query to list the organization's suppliers, searched and paged.</summary>
public sealed record SupplierGetAllQuery : IQuery<AppResult<IReadOnlyList<SupplierSummary>>>
{
    /// <summary>Gets the caller's resolved organization membership.</summary>
    public required OrganizationAccess Access { get; init; }

    /// <summary>Gets the text to search for.</summary>
    public string? Query { get; init; }

    /// <summary>Gets the stage to keep.</summary>
    public string? Stage { get; init; }

    /// <summary>Gets the largest number of suppliers to return.</summary>
    public required int Limit { get; init; }

    /// <summary>Gets the number of suppliers to skip.</summary>
    public required int Offset { get; init; }
}
