namespace OcharoCatalogue.Domain.Catalogue.Models;

/// <summary>A quantity band and its unit price; <see cref="MaximumQuantity"/> null means open-ended.</summary>
public sealed record PriceTier(int MinimumQuantity, int? MaximumQuantity, long UnitPrice);
