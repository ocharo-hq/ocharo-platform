using OcharoCatalogue.Application.Catalogue.Publishing.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Application.Catalogue.Publishing.Queries;

/// <summary>Represents a query to get one artifact of a published release.</summary>
public sealed record EditionAssetGetQuery : IQuery<AppResult<CataloguePublicAsset>>
{
    /// <summary>Gets the catalogue slug.</summary>
    public required string Slug { get; init; }

    /// <summary>Gets the edition's language.</summary>
    public required string Locale { get; init; }

    /// <summary>Gets the release id.</summary>
    public required Guid ReleaseId { get; init; }

    /// <summary>Gets the artifact's name, such as <c>catalogue.pdf</c>.</summary>
    public required string Asset { get; init; }
}
