namespace OcharoCatalogue.Domain.Catalogue.Models;

/// <summary>Per-piece pricing by order quantity.</summary>
public sealed record ProductPricing(string Currency, string Status, IReadOnlyList<PriceTier> Tiers, string? Wording)
{
    /// <summary>The single-piece (retail) unit price, if a tier starts at one.</summary>
    public long? RetailPrice => Tiers.FirstOrDefault(t => t.MinimumQuantity == 1)?.UnitPrice;

    /// <summary>The lowest unit price across all tiers (wholesale floor).</summary>
    public long? WholesalePrice => Tiers.Count == 0 ? null : Tiers.Min(t => t.UnitPrice);
}
