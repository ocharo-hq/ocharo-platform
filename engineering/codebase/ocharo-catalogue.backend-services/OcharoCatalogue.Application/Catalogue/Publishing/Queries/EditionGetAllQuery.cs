using OcharoCatalogue.Application.Catalogue.Publishing.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Application.Catalogue.Publishing.Queries;

/// <summary>Represents a query to get the published editions of one language.</summary>
public sealed record EditionGetAllQuery : IQuery<AppResult<IReadOnlyList<PublishedEditionSummary>>>
{
    /// <summary>Gets the editions' language; Uzbek when null.</summary>
    public string? Locale { get; init; }
}
