using BrandWorkspace.Application.Access.Models;
using BrandWorkspace.Application.Files.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Application.Commerce.Queries;

/// <summary>Represents a query to read a brand's private media bytes.</summary>
public sealed record CommerceMediaReadQuery : IQuery<AppResult<StoredContent>>
{
    /// <summary>Gets the caller's resolved organization membership.</summary>
    public required OrganizationAccess Access { get; init; }

    /// <summary>Gets the brand.</summary>
    public required Guid BrandId { get; init; }

    /// <summary>Gets the media's SHA-256.</summary>
    public required string Sha256 { get; init; }
}
