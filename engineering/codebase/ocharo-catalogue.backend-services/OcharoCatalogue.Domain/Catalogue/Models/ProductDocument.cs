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

/// <summary>Catalogue copy: original fields remain wire compatible; further languages use Additional.</summary>
public sealed record ProductLocales(ProductLocale Uz, ProductLocale Ru, ProductLocale En)
{
    public IReadOnlyDictionary<string, ProductLocale> Additional { get; init; } = new Dictionary<string, ProductLocale>(StringComparer.Ordinal);

    /// <summary>Returns exact-language copy; an absent translation is an empty draft, never another language.</summary>
    public ProductLocale For(string? locale) => locale switch
    {
        null or "uz" => Uz ?? ProductLocale.Empty(),
        "ru" => Ru ?? ProductLocale.Empty(),
        "en" => En ?? ProductLocale.Empty(),
        _ => Additional?.GetValueOrDefault(locale) ?? ProductLocale.Empty(),
    };

    public static ProductLocales Empty() => new(ProductLocale.Empty(), ProductLocale.Empty(), ProductLocale.Empty());
}

/// <summary>Copy for one language; <see cref="Status"/> is draft until the owner approves the wording.</summary>
public sealed record ProductLocale(string Status, string? Name, string? Fabric, string? Decoration, string? Description)
{
    /// <summary>An empty draft locale.</summary>
    public static ProductLocale Empty() => new(LocaleStatus.Draft, null, null, null, null);
}

/// <summary>Per-piece pricing by order quantity.</summary>
public sealed record ProductPricing(string Currency, string Status, IReadOnlyList<PriceTier> Tiers, string? Wording)
{
    /// <summary>The single-piece (retail) unit price, if a tier starts at one.</summary>
    public long? RetailPrice => Tiers.FirstOrDefault(t => t.MinimumQuantity == 1)?.UnitPrice;

    /// <summary>The lowest unit price across all tiers (wholesale floor).</summary>
    public long? WholesalePrice => Tiers.Count == 0 ? null : Tiers.Min(t => t.UnitPrice);
}

/// <summary>A quantity band and its unit price; <see cref="MaximumQuantity"/> null means open-ended.</summary>
public sealed record PriceTier(int MinimumQuantity, int? MaximumQuantity, long UnitPrice);

/// <summary>An image attached to a product; bytes live in the content-addressed media store.</summary>
public sealed record ProductMedia(
    string Id,
    string Role,
    string FileName,
    string ContentType,
    long ByteLength,
    string Sha256,
    int? Width,
    int? Height,
    bool HasPreview,
    string? Alt,
    string? SourcePath,
    bool IsActive = true,
    string? Group = null);

/// <summary>A review question about one field; resolved issues stay as evidence.</summary>
public sealed record ReviewIssue(string Id, string Field, string Status, string Question);

/// <summary>Import provenance: the file and hash a record was read from.</summary>
public sealed record ProductSource(string Kind, string? Path, string? Sha256, DateTimeOffset? ImportedAt);
