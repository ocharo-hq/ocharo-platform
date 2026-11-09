using BrandWorkspace.Application.Files.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Application.Commerce.Queries;

/// <summary>Represents a query to open a released brand's approved media.</summary>
public sealed record PublicMediaOpenQuery : IQuery<AppResult<StoredContent>>
{
    /// <summary>Gets the brand's public slug.</summary>
    public required string Slug { get; init; }

    /// <summary>Gets the media's SHA-256.</summary>
    public required string Sha256 { get; init; }

    /// <summary>Gets the release to read; the latest when absent.</summary>
    public Guid? Release { get; init; }
}
