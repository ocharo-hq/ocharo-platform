using OcharoCatalogue.Application.Catalogue.Products.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Application.Catalogue.Products.Commands;

/// <summary>Represents a command to copy a product into the next draft variant.</summary>
public sealed record ProductDuplicateCommand : ICommand<AppResult<ProductDetail>>
{
    /// <summary>Gets the id of the product to copy.</summary>
    public required string Id { get; init; }
}
