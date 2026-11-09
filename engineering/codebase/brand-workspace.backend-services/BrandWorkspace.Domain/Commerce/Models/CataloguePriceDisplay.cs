namespace BrandWorkspace.Domain.Commerce.Models;

/// <summary>Represents the price facts a catalogue shows.</summary>
public sealed record CataloguePriceDisplay(
    bool Show,
    bool ShowCurrency,
    bool ShowQuantity,
    bool ShowRetail,
    bool ShowWholesale);
