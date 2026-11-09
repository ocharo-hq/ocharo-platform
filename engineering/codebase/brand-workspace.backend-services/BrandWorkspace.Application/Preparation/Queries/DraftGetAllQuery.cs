using BrandWorkspace.Application.Access.Models;
using BrandWorkspace.Domain.Preparation.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Application.Preparation.Queries;

/// <summary>Represents a query to list the organization's drafts, optionally of one brand.</summary>
public sealed record DraftGetAllQuery : IQuery<AppResult<IReadOnlyList<DraftSummary>>>
{
    /// <summary>Gets the caller's resolved organization membership.</summary>
    public required OrganizationAccess Access { get; init; }

    /// <summary>Gets the brand whose drafts to list, if any.</summary>
    public Guid? BrandId { get; init; }
}
