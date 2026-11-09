using OcharoCatalogue.Application.Catalogue.Products.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Application.Catalogue.Products.Commands;

/// <summary>Represents a command to create a product.</summary>
public sealed record ProductCreateCommand : ICommand<AppResult<ProductDetail>>
{
    /// <summary>Gets the product fields.</summary>
    public required ProductInput Input { get; init; }
}
