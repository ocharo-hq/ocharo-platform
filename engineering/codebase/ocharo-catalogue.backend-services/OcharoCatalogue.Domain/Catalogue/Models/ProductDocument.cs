namespace OcharoCatalogue.Domain.Catalogue.Models;

/// <summary>
/// One catalogue product (a garment variant or a styled look) as a single versioned document.
/// Every save stores a new revision; field-level review state travels with the document.
/// </summary>
public sealed record ProductDocument
{
    /// <summary>Stable slug, e.g. <c>nimcha-v06</c>. Never reused.</summary>
    public required string Id { get; init; }

    /// <summary>Garment family key from the catalogue settings (<c>nimcha</c>, <c>baska</c>, <c>look</c>, …).</summary>
    public required string GarmentType { get; init; }

    /// <summary>Variant code within the garment family, e.g. <c>v06</c>.</summary>
    public required string VariantCode { get; init; }

    /// <summary>Optional design family shared by several colourways.</summary>
    public string? DesignId { get; init; }

    /// <summary><see cref="ProductStatus"/>: draft, active or archived.</summary>
    public required string Status { get; init; }

    /// <summary>Manual catalogue order; lower first.</summary>
    public int SortOrder { get; init; }

    /// <summary>Offered sizes in display order; empty when not yet confirmed.</summary>
    public IReadOnlyList<string> Sizes { get; init; } = [];

    /// <summary>Fabric codes (free vocabulary until a fabric registry exists).</summary>
    public IReadOnlyList<string> FabricCodes { get; init; } = [];

    /// <summary>Decoration codes (embroidery, trim, …).</summary>
    public IReadOnlyList<string> DecorationCodes { get; init; } = [];

    /// <summary>Operator tags used for filtering and collections.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>Localized copy, retaining the original Uzbek, Russian and English fields.</summary>
    public required ProductLocales Locales { get; init; }

    /// <summary>Quantity-tier pricing; null when no price is recorded.</summary>
    public ProductPricing? Pricing { get; init; }

    /// <summary>Images attached to the product, in display order.</summary>
    public IReadOnlyList<ProductMedia> Media { get; init; } = [];

    /// <summary>Open and resolved review questions, field by field.</summary>
    public IReadOnlyList<ReviewIssue> Issues { get; init; } = [];

    /// <summary>The original supplied wording, preserved verbatim.</summary>
    public string? SourceVerbatim { get; init; }

    /// <summary>Where this record came from when it was imported.</summary>
    public ProductSource? Source { get; init; }

    /// <summary>Internal operator notes; never published.</summary>
    public string? Notes { get; init; }

    /// <summary>Pieces on hand per size; null when stock is not tracked for this product.</summary>
    public IReadOnlyDictionary<string, int>? Stock { get; init; }

    /// <summary>Sewn after an order is placed; availability then ignores stock.</summary>
    public bool MadeToOrder { get; init; }

    /// <summary>
    /// <c>made-to-order</c>, <c>in-stock</c>, <c>sold-out</c> or <c>unknown</c> (not tracked). Exact counts stay internal.
    /// </summary>
    public string Availability =>
        MadeToOrder ? "made-to-order"
        : Stock is null ? "unknown"
        : Stock.Values.Sum() > 0 ? "in-stock"
        : "sold-out";

    /// <summary>Sizes with at least one piece on hand (all sizes when made to order).</summary>
    public IReadOnlyList<string> AvailableSizes =>
        MadeToOrder ? Sizes : Stock is null ? [] : Sizes.Where(s => Stock.GetValueOrDefault(s) > 0).ToList();
}
