using OcharoCatalogue.Domain.Catalogue.Models;

namespace OcharoCatalogue.Application.Catalogue.Products.Models;

/// <summary>
/// Represents the editable product fields; media bytes are uploaded separately and only referenced here.
/// </summary>
public sealed record ProductInput
{
    /// <summary>Gets the revision the caller edited; required for an update.</summary>
    public int? ExpectedRevision { get; init; }

    /// <summary>Gets the product id; derived from the garment type and variant when empty.</summary>
    public string? Id { get; init; }

    /// <summary>Gets the garment family key.</summary>
    public required string GarmentType { get; init; }

    /// <summary>Gets the variant code; the next free one when empty.</summary>
    public string? VariantCode { get; init; }

    /// <summary>Gets the design family shared by several colourways.</summary>
    public string? DesignId { get; init; }

    /// <summary>Gets the product status.</summary>
    public string Status { get; init; } = ProductStatus.Draft;

    /// <summary>Gets the manual catalogue order.</summary>
    public int SortOrder { get; init; }

    /// <summary>Gets the offered sizes.</summary>
    public IReadOnlyList<string>? Sizes { get; init; }

    /// <summary>Gets the fabric codes.</summary>
    public IReadOnlyList<string>? FabricCodes { get; init; }

    /// <summary>Gets the decoration codes.</summary>
    public IReadOnlyList<string>? DecorationCodes { get; init; }

    /// <summary>Gets the operator tags.</summary>
    public IReadOnlyList<string>? Tags { get; init; }

    /// <summary>Gets the localized copy; the current copy stays when null.</summary>
    public ProductLocales? Locales { get; init; }

    /// <summary>Gets the quantity-tier pricing.</summary>
    public ProductPricing? Pricing { get; init; }

    /// <summary>Gets the uploaded images in display order; the current images stay when null.</summary>
    public IReadOnlyList<MediaInput>? Media { get; init; }

    /// <summary>Gets the review questions.</summary>
    public IReadOnlyList<ReviewIssue>? Issues { get; init; }

    /// <summary>Gets the internal operator notes.</summary>
    public string? Notes { get; init; }

    /// <summary>Gets the pieces on hand per size; null stops tracking.</summary>
    public IReadOnlyDictionary<string, int>? Stock { get; init; }

    /// <summary>Gets whether the product is sewn after an order is placed.</summary>
    public bool MadeToOrder { get; init; }

    /// <summary>Gets the history line of the save; derived from the changes when empty.</summary>
    public string? ChangeSummary { get; init; }
}
