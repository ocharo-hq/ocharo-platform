using OcharoCatalogue.Domain.Catalogue.Models;

namespace OcharoCatalogue.Application.Catalogue.Publishing.Models;

/// <summary>Represents an active product with copy in the requested locale plus every approved locale.</summary>
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
