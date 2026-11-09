using BrandWorkspace.Application.Commerce.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Application.Commerce.Queries;

/// <summary>
/// Represents a query to open a released edition's PDF or page artifact; the release check decides each time.
/// </summary>
public sealed record PublicEditionAssetOpenQuery : IQuery<AppResult<PublicAsset>>
{
    /// <summary>Gets the brand's public slug.</summary>
    public required string Slug { get; init; }

    /// <summary>Gets the edition.</summary>
    public required Guid EditionId { get; init; }

    /// <summary>Gets the artifact's name: catalogue.pdf, or a page's png or pdf.</summary>
    public required string Asset { get; init; }

    /// <summary>Gets the release to read.</summary>
    public Guid? Release { get; init; }

    /// <summary>Gets whether a download saves it under its name.</summary>
    public required bool Download { get; init; }
}
