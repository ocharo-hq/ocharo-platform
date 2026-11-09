using BrandWorkspace.Application.Access.Models;
using BrandWorkspace.Domain.Commerce;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Application.Commerce.Queries;

/// <summary>Represents a query to list a brand's releases.</summary>
public sealed record ReleaseGetAllQuery : IQuery<AppResult<IReadOnlyList<CommerceRelease>>>
{
    /// <summary>Gets the caller's resolved organization membership.</summary>
    public required OrganizationAccess Access { get; init; }

    /// <summary>Gets the brand.</summary>
    public required Guid BrandId { get; init; }
}
