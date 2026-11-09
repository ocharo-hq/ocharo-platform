using OcharoCatalogue.Application.Catalogue.Products.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Application.Catalogue.Products.Queries;

/// <summary>Represents a query to get a product's saved revisions, newest first.</summary>
public sealed record ProductRevisionGetAllQuery : IQuery<AppResult<IReadOnlyList<RevisionSummary>>>
{
    /// <summary>Gets the product id.</summary>
    public required string Id { get; init; }
}
