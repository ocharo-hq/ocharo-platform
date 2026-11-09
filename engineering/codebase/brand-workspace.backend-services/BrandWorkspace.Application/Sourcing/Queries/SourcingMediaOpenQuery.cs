using BrandWorkspace.Application.Access.Models;
using BrandWorkspace.Application.Files.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Application.Sourcing.Queries;

/// <summary>Represents a query to open a media item's original: a WoW2 Files link, or its bytes.</summary>
public sealed record SourcingMediaOpenQuery : IQuery<AppResult<StoredContent>>
{
    /// <summary>Gets the caller's resolved organization membership.</summary>
    public required OrganizationAccess Access { get; init; }

    /// <summary>Gets the media item to open.</summary>
    public required Guid MediaId { get; init; }
}
