using OcharoCatalogue.Application.Catalogue.Products.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Application.Catalogue.Products.Commands;

/// <summary>Represents a command to store an uploaded image and append it to a product as a new revision.</summary>
public sealed record ProductMediaAddCommand : ICommand<AppResult<ProductDetail>>
{
    /// <summary>Gets the product id.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the uploaded bytes.</summary>
    public required byte[] Bytes { get; init; }

    /// <summary>Gets the uploaded file's name.</summary>
    public required string FileName { get; init; }

    /// <summary>Gets what the image shows.</summary>
    public required string Role { get; init; }

    /// <summary>Gets the revision the caller saw; unchecked when null.</summary>
    public int? ExpectedRevision { get; init; }
}
