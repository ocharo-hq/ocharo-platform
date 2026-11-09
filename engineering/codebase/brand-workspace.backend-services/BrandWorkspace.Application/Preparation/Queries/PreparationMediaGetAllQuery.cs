using BrandWorkspace.Application.Access.Models;
using BrandWorkspace.Domain.Preparation.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Application.Preparation.Queries;

/// <summary>Represents a query to list the organization's preparation images.</summary>
public sealed record PreparationMediaGetAllQuery : IQuery<AppResult<IReadOnlyList<MediaAsset>>>
{
    /// <summary>Gets the caller's resolved organization membership.</summary>
    public required OrganizationAccess Access { get; init; }
}
