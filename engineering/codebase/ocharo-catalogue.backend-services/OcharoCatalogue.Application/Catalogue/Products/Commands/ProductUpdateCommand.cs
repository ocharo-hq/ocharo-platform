using OcharoCatalogue.Application.Catalogue.Products.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Application.Catalogue.Products.Commands;

/// <summary>Represents a command to replace a product's editable fields at the revision the caller edited.</summary>
public sealed record ProductUpdateCommand : ICommand<AppResult<ProductDetail>>
{
    /// <summary>Gets the product id.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the product fields.</summary>
    public required ProductInput Input { get; init; }
}
