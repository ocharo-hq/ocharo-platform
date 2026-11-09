using OcharoCatalogue.Application.Catalogue.Designs.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Application.Catalogue.Designs.Queries;

/// <summary>Represents a query to get the catalogue designs for the gallery.</summary>
public sealed record DesignGetAllQuery : IQuery<AppResult<IReadOnlyList<CatalogueDesignSummary>>>
{
    /// <summary>Gets whether archived designs are listed too.</summary>
    public required bool Archived { get; init; }
}
