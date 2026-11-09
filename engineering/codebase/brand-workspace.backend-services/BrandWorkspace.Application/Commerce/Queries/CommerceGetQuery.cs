using BrandWorkspace.Application.Access.Models;
using BrandWorkspace.Application.Commerce.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Application.Commerce.Queries;

/// <summary>Represents a query to get a brand's commerce draft, or none before its first save.</summary>
public sealed record CommerceGetQuery : IQuery<AppResult<CommerceDraftLookup>>
{
    /// <summary>Gets the caller's resolved organization membership.</summary>
    public required OrganizationAccess Access { get; init; }

    /// <summary>Gets the brand.</summary>
    public required Guid BrandId { get; init; }
}
