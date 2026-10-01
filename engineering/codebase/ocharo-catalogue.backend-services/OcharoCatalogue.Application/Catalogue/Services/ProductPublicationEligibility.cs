using OcharoCatalogue.Domain.Catalogue.Models;

namespace OcharoCatalogue.Application.Catalogue.Services;

/// <summary>One exact-language publication boundary for feeds and immutable catalogue preparation.</summary>
public static class ProductPublicationEligibility
{
    public static bool Eligible(ProductDocument product, string locale) =>
        CatalogueLocales.Supported(locale) && product.Status == ProductStatus.Active
        && product.Locales.For(locale) is { Status: LocaleStatus.Approved, Name: { } name }
        && !string.IsNullOrWhiteSpace(name)
        && ActiveImages(product).Count > 0
        && !product.Issues.Any(issue => issue.Status == IssueStatus.Open);

    public static IReadOnlyList<ProductMedia> ActiveImages(ProductDocument product) =>
        product.Media.Where(image => image.IsActive && image.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)).ToList();

    public static string MediaGroup(ProductMedia image) => ProductMediaGroup.For(image);

    /// <summary>Legacy non-overlapping bands remain readable; malformed or unconfirmed prices stay private.</summary>
    public static bool PublicPricing(ProductPricing? pricing) =>
        pricing is { Status: PricingStatus.Confirmed, Currency: "UZS" } && ValidTiers(pricing.Tiers);

    public static bool ValidTiers(IReadOnlyList<PriceTier>? tiers)
    {
        if (tiers is null || tiers.Count is < 1 or > 100) return false;
        var ordered = tiers.OrderBy(tier => tier.MinimumQuantity).ToList();
        for (var index = 0; index < ordered.Count; index++)
        {
            var tier = ordered[index];
            if (tier.MinimumQuantity < 1 || tier.MaximumQuantity < tier.MinimumQuantity || tier.UnitPrice is < 0 or > 1_000_000_000) return false;
            if (index + 1 < ordered.Count && (tier.MaximumQuantity is null || tier.MaximumQuantity >= ordered[index + 1].MinimumQuantity)) return false;
        }
        return true;
    }

    /// <summary>New and changed price sheets use the two agreed quantity bands.</summary>
    public static bool StandardBands(IReadOnlyList<PriceTier> tiers) => tiers.Count == 2
        && tiers.Any(tier => tier.MinimumQuantity == 1 && tier.MaximumQuantity == 9)
        && tiers.Any(tier => tier.MinimumQuantity == 10 && tier.MaximumQuantity is null);
}
