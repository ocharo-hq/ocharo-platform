namespace OcharoCatalogue.Application.Catalogue.Products.Models;

/// <summary>
/// Represents a price change on several products: <c>percent</c> multiplies every tier by (100 + Value)/100,
/// <c>add</c> adds Value to every tier; results round to <see cref="RoundTo"/> (e.g. 1000 UZS).
/// </summary>
public sealed record BulkPricingInput(
    IReadOnlyList<string> ProductIds,
    string Mode,
    decimal Value,
    int RoundTo = 1000,
    string? Tier = null);
