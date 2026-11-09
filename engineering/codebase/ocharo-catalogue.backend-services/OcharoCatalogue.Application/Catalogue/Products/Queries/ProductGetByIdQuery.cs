using OcharoCatalogue.Application.Catalogue.Products.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Application.Catalogue.Products.Queries;

/// <summary>Represents a query to get one product with its media and collections.</summary>
public sealed record ProductGetByIdQuery : IQuery<AppResult<ProductDetail>>
{
    /// <summary>Gets the product id.</summary>
    public required string Id { get; init; }
}
