namespace OcharoCatalogue.Application.Catalogue.Products.Models;

/// <summary>Represents a product row for grids and pickers.</summary>
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
