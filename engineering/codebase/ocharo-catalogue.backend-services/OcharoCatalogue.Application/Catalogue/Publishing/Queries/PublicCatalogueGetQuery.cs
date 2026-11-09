using OcharoCatalogue.Application.Catalogue.Publishing.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Application.Catalogue.Publishing.Queries;

/// <summary>
/// Represents a query to get the public feed: active products and published collections in one language.
/// </summary>
public sealed record PublicCatalogueGetQuery : IQuery<AppResult<PublicCatalogue>>
{
    /// <summary>Gets the feed's language; Uzbek when null.</summary>
    public string? Locale { get; init; }
}
