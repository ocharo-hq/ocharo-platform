using OcharoCatalogue.Domain.Catalogue.Models;

namespace OcharoCatalogue.Application.Catalogue.Publishing.Models;

/// <summary>
/// Represents the public, read-only catalogue feed consumed by Marketing, the Blog and print exports.
/// </summary>
public sealed record PublicCatalogue(
    string Brand,
    string Currency,
    string Locale,
    DateTimeOffset GeneratedAt,
    string? PriceNote,
    IReadOnlyList<GarmentTypeDefinition> GarmentTypes,
    IReadOnlyList<PublicCollection> Collections,
    IReadOnlyList<PublicProduct> Products);
