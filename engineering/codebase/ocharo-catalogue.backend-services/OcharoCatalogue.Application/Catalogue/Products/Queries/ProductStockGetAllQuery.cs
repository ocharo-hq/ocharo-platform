using OcharoCatalogue.Application.Catalogue.Products.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Application.Catalogue.Products.Queries;

/// <summary>Represents a query to get the stock sheet: every product's sizes, pieces and availability.</summary>
public sealed record ProductStockGetAllQuery : IQuery<AppResult<IReadOnlyList<StockRow>>>;
