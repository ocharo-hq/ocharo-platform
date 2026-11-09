using OcharoCatalogue.Application.Catalogue.Products.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Application.Catalogue.Products.Queries;

/// <summary>Represents a query to get every product's copy for the translation grid, in catalogue order.</summary>
public sealed record ProductCopyGetAllQuery : IQuery<AppResult<IReadOnlyList<ProductCopyRow>>>;
