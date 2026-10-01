using OcharoCatalogue.Domain.Catalogue.Models;

namespace OcharoCatalogue.Application.Catalogue.Models;

/// <summary>The public, read-only catalogue feed consumed by Marketing, the Blog and print exports.</summary>
public sealed record PublicCatalogue(
    string Brand,
    string Currency,
    string Locale,
    DateTimeOffset GeneratedAt,
    string? PriceNote,
    IReadOnlyList<GarmentTypeDefinition> GarmentTypes,
    IReadOnlyList<PublicCollection> Collections,
    IReadOnlyList<PublicProduct> Products);

/// <summary>A published collection.</summary>
public sealed record PublicCollection(string Id, string Name, CollectionNames Names, string? Description, IReadOnlyList<string> ProductIds);

/// <summary>An active product with copy in the requested locale plus every locale for consumers that need them.</summary>
public sealed record PublicProduct(
    string Id,
    string GarmentType,
    string GarmentName,
    string VariantCode,
    string? Name,
    string? Fabric,
    string? Decoration,
    string? Description,
    string LocaleStatus,
    ProductLocales Locales,
    IReadOnlyList<string> Sizes,
    PublicPricing? Pricing,
    IReadOnlyList<PublicImage> Images,
    IReadOnlyList<string> Tags,
    int Revision,
    DateTimeOffset UpdatedAt,
    string Availability = "unknown",
    IReadOnlyList<string>? AvailableSizes = null);

/// <summary>Public pricing view.</summary>
public sealed record PublicPricing(string Currency, string Status, long? Retail, long? Wholesale, IReadOnlyList<PriceTier> Tiers, string? Wording);

/// <summary>A public image with absolute-path URLs and integrity hash.</summary>
public sealed record PublicImage(string Role, string Url, string? PreviewUrl, int? Width, int? Height, string? Alt, string Sha256, string? Group = null, bool IsActive = true);
