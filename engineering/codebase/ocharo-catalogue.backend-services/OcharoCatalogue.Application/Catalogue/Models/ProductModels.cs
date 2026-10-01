using OcharoCatalogue.Domain.Catalogue.Models;

namespace OcharoCatalogue.Application.Catalogue.Models;

/// <summary>A product row for grids and pickers.</summary>
public sealed record ProductSummary(
    string Id,
    string GarmentType,
    string VariantCode,
    string Status,
    int SortOrder,
    string? NameUz,
    string? NameRu,
    string? NameEn,
    long? RetailPrice,
    long? WholesalePrice,
    string? PricingStatus,
    int LocalesApproved,
    int OpenIssues,
    int MediaCount,
    string? PrimaryImageUrl,
    string? PrimaryPreviewUrl,
    IReadOnlyList<string> Tags,
    int Revision,
    DateTimeOffset UpdatedAt,
    string Availability = "unknown",
    int? StockTotal = null,
    int LanguagesTotal = 3,
    int UnconfirmedTranslations = 3,
    IReadOnlyList<string>? CollectionIds = null);

/// <summary>A full product with its revision stamp and resolved media URLs.</summary>
public sealed record ProductDetail(
    ProductDocument Document,
    int Revision,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<MediaView> Media,
    IReadOnlyList<string> CollectionIds);

/// <summary>A media item with its original and preview URLs.</summary>
public sealed record MediaView(ProductMedia Media, string Url, string? PreviewUrl);

/// <summary>Editable product fields; media bytes are uploaded separately and only referenced here.</summary>
public sealed record ProductInput
{
    public int? ExpectedRevision { get; init; }
    public string? Id { get; init; }
    public required string GarmentType { get; init; }
    public string? VariantCode { get; init; }
    public string? DesignId { get; init; }
    public string Status { get; init; } = ProductStatus.Draft;
    public int SortOrder { get; init; }
    public IReadOnlyList<string>? Sizes { get; init; }
    public IReadOnlyList<string>? FabricCodes { get; init; }
    public IReadOnlyList<string>? DecorationCodes { get; init; }
    public IReadOnlyList<string>? Tags { get; init; }
    public ProductLocales? Locales { get; init; }
    public ProductPricing? Pricing { get; init; }
    public IReadOnlyList<MediaInput>? Media { get; init; }
    public IReadOnlyList<ReviewIssue>? Issues { get; init; }
    public string? Notes { get; init; }
    public IReadOnlyDictionary<string, int>? Stock { get; init; }
    public bool MadeToOrder { get; init; }
    public string? ChangeSummary { get; init; }
}

/// <summary>One row of the stock sheet.</summary>
public sealed record StockRow(string Id, string GarmentType, string Status, int Revision, string? NameUz, string? PreviewUrl, IReadOnlyList<string> Sizes,
    IReadOnlyDictionary<string, int>? Stock, bool MadeToOrder, string Availability);

/// <summary>Replaces only stock tracking (revision-checked); a null <see cref="Stock"/> stops tracking.</summary>
public sealed record StockInput(int ExpectedRevision, IReadOnlyDictionary<string, int>? Stock, bool MadeToOrder);

/// <summary>Reorders/relabels an existing media item; order in the list is display order.</summary>
public sealed record MediaInput(string Id, string Role, string? Alt, bool? IsActive = null, string? Group = null);

/// <summary>Grid filters.</summary>
public sealed record ProductFilter(string? Query, string? GarmentType, string? Status, string? Collection, string? Tag);

/// <summary>A saved revision in the history list.</summary>
public sealed record RevisionSummary(int Revision, DateTimeOffset SavedAt, string Summary);

/// <summary>Sets one status on several products.</summary>
public sealed record BulkStatusInput(IReadOnlyList<string> ProductIds, string Status);

/// <summary>
/// Adjusts prices on several products: <c>percent</c> multiplies every tier by (100 + Value)/100,
/// <c>add</c> adds Value to every tier; results round to <see cref="RoundTo"/> (e.g. 1000 UZS).
/// </summary>
public sealed record BulkPricingInput(IReadOnlyList<string> ProductIds, string Mode, decimal Value, int RoundTo = 1000, string? Tier = null);

/// <summary>Outcome of a bulk command.</summary>
public sealed record BulkResult(int Changed, IReadOnlyList<string> Skipped);

/// <summary>One row of the copy grid: every language's copy for a product.</summary>
public sealed record ProductCopyRow(string Id, string GarmentType, string Status, int Revision, string? PreviewUrl, ProductLocales Locales);

/// <summary>Replaces only the localized copy of a product (revision-checked).</summary>
public sealed record CopyInput(int ExpectedRevision, ProductLocales Locales);
