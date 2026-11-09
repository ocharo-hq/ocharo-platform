using OcharoCatalogue.Domain.Catalogue.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Application.Catalogue.Products.Queries;

/// <summary>Represents a query to get the document a product had at one saved revision.</summary>
public sealed record ProductRevisionGetByIdQuery : IQuery<AppResult<ProductDocument>>
{
    /// <summary>Gets the product id.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the revision number.</summary>
    public required int Revision { get; init; }
}
