using OcharoCatalogue.Application.Catalogue.Products.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Application.Catalogue.Products.Commands;

/// <summary>Represents a command to save only a product's stock tracking.</summary>
public sealed record ProductStockUpdateCommand : ICommand<AppResult<StockRow>>
{
    /// <summary>Gets the product id.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the stock change.</summary>
    public required StockInput Input { get; init; }
}
