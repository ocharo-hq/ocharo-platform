using OcharoCatalogue.Application.Catalogue.Products.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Application.Catalogue.Products.Commands;

/// <summary>
/// Represents a command to save a product's localized copy; omitted additional languages keep their values.
/// </summary>
public sealed record ProductCopyUpdateCommand : ICommand<AppResult<ProductCopyRow>>
{
    /// <summary>Gets the product id.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the copy change.</summary>
    public required CopyInput Input { get; init; }
}
