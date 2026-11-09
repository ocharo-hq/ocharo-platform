using OcharoCatalogue.Application.Catalogue.Products.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Application.Catalogue.Products.Queries;

/// <summary>Represents a query to get the product grid's rows.</summary>
public sealed record ProductGetAllQuery : IQuery<AppResult<IReadOnlyList<ProductSummary>>>
{
    /// <summary>Gets the grid's filters.</summary>
    public required ProductFilter Filter { get; init; }
}
