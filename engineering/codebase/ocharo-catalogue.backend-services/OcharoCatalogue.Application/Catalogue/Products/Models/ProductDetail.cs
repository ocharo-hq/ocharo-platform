using OcharoCatalogue.Domain.Catalogue.Models;

namespace OcharoCatalogue.Application.Catalogue.Products.Models;

/// <summary>Represents a full product with its revision stamp and resolved media URLs.</summary>
public sealed record ProductDetail(
    ProductDocument Document,
    int Revision,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<MediaView> Media,
    IReadOnlyList<string> CollectionIds);
