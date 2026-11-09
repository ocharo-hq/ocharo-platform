using BrandWorkspace.Application.Access.Models;
using BrandWorkspace.Domain.Sourcing.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Application.Sourcing.Queries;

/// <summary>Represents a query to list the organization's supplier media, by supplier or inbox, paged.</summary>
public sealed record SourcingMediaGetAllQuery : IQuery<AppResult<IReadOnlyList<SourcingMedia>>>
{
    /// <summary>Gets the caller's resolved organization membership.</summary>
    public required OrganizationAccess Access { get; init; }

    /// <summary>Gets the supplier whose media to list, if any.</summary>
    public Guid? SupplierId { get; init; }

    /// <summary>Gets whether to list only the inbox.</summary>
    public required bool Unassigned { get; init; }

    /// <summary>Gets the largest number of items to return.</summary>
    public required int Limit { get; init; }

    /// <summary>Gets the number of items to skip.</summary>
    public required int Offset { get; init; }
}
