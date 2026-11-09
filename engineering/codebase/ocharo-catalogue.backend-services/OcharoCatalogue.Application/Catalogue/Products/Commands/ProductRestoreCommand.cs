using OcharoCatalogue.Application.Catalogue.Products.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Application.Catalogue.Products.Commands;

/// <summary>
/// Represents a command to save an older snapshot as the newest revision; history is never rewritten.
/// </summary>
public sealed record ProductRestoreCommand : ICommand<AppResult<ProductDetail>>
{
    /// <summary>Gets the product id.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the revision to restore.</summary>
    public required int Revision { get; init; }

    /// <summary>Gets the revision the caller edited.</summary>
    public required int ExpectedRevision { get; init; }
}
