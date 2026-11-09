using OcharoCatalogue.Application.Catalogue.Products.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Application.Catalogue.Products.Commands;

/// <summary>Represents a command to adjust the prices of several products.</summary>
public sealed record ProductPricingBulkAdjustCommand : ICommand<AppResult<BulkResult>>
{
    /// <summary>Gets the products and the price change.</summary>
    public required BulkPricingInput Input { get; init; }
}
