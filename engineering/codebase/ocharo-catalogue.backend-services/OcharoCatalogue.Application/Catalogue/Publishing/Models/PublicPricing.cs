using OcharoCatalogue.Domain.Catalogue.Models;

namespace OcharoCatalogue.Application.Catalogue.Publishing.Models;

/// <summary>Represents the public pricing of a product.</summary>
public sealed record PublicPricing(
    string Currency,
    string Status,
    long? Retail,
    long? Wholesale,
    IReadOnlyList<PriceTier> Tiers,
    string? Wording);
