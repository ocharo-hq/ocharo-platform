using OcharoCatalogue.Domain.Catalogue.Models;

namespace OcharoCatalogue.Infrastructure.Catalogue.Products.Models;

/// <summary>Represents the parts of a stored product an edit keeps: its images, import provenance, and the copy and
/// pricing it falls back to.</summary>
/// <param name="Media">The stored images.</param>
/// <param name="SourceVerbatim">The original supplied wording.</param>
/// <param name="Source">The import provenance.</param>
/// <param name="Locales">The stored copy; null for a new product.</param>
/// <param name="Pricing">The stored pricing; null for a new product.</param>
public sealed record ProductCurrentParts(
    IReadOnlyList<ProductMedia> Media,
    string? SourceVerbatim,
    ProductSource? Source,
    ProductLocales? Locales = null,
    ProductPricing? Pricing = null)
{
    /// <summary>Holds the parts of a product that does not exist yet.</summary>
    public static readonly ProductCurrentParts None = new(
        [],
        null,
        null);
}
