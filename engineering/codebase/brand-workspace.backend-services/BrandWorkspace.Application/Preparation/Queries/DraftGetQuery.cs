using BrandWorkspace.Application.Access.Models;
using BrandWorkspace.Domain.Preparation.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Application.Preparation.Queries;

/// <summary>Represents a query to get a draft at its current revision.</summary>
public sealed record DraftGetQuery : IQuery<AppResult<Draft>>
{
    /// <summary>Gets the caller's resolved organization membership.</summary>
    public required OrganizationAccess Access { get; init; }

    /// <summary>Gets the draft to read.</summary>
    public required Guid DraftId { get; init; }
}
