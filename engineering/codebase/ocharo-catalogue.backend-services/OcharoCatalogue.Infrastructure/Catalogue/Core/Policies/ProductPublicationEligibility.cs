using OcharoCatalogue.Domain.Catalogue.Models;

namespace OcharoCatalogue.Infrastructure.Catalogue.Core.Policies;

/// <summary>
/// Decides the one exact-language publication boundary for feeds and immutable catalogue preparation.
/// </summary>
public static class ProductPublicationEligibility
{
    /// <summary>Tells whether <paramref name="product"/> may be published in <paramref name="locale"/>.</summary>
    /// <param name="product">The product.</param>
    /// <param name="locale">The catalogue language.</param>
    public static bool Eligible(ProductDocument product, string locale)
    {
        return CatalogueLocales.Supported(locale)
            && product.Status == ProductStatus.Active
            && product.Locales.For(locale) is { Status: LocaleStatus.Approved, Name: { } name }
            && !string.IsNullOrWhiteSpace(name)
            && ActiveImages(product).Count > 0
            && !product.Issues.Any(issue => issue.Status == IssueStatus.Open);
    }

    /// <summary>Gets the product's active images, in display order.</summary>
    /// <param name="product">The product.</param>
    public static IReadOnlyList<ProductMedia> ActiveImages(ProductDocument product)
    {
        return product.Media
            .Where(
                image => image.IsActive && image.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>Gets the image group a catalogue page selects <paramref name="image"/> by.</summary>
    /// <param name="image">The image.</param>
    public static string MediaGroup(ProductMedia image)
    {
        return ProductMediaGroup.For(image);
    }

    /// <summary>Tells whether a price may be shown publicly; legacy non-overlapping bands remain readable, malformed or
    /// unconfirmed prices stay private.</summary>
    /// <param name="pricing">The product's pricing.</param>
    public static bool PublicPricing(ProductPricing? pricing)
    {
        return pricing is { Status: PricingStatus.Confirmed, Currency: "UZS" } && ValidTiers(pricing.Tiers);
    }

    /// <summary>Tells whether <paramref name="tiers"/> are well-formed, non-overlapping bands.</summary>
    /// <param name="tiers">The price tiers.</param>
    public static bool ValidTiers(IReadOnlyList<PriceTier>? tiers)
    {
        if (tiers is null || tiers.Count is < 1 or > 100)
        {
            return false;
        }

        var ordered = tiers.OrderBy(tier => tier.MinimumQuantity).ToList();
        for (var index = 0; index < ordered.Count; index++)
        {
            var tier = ordered[index];
            if (tier.MinimumQuantity < 1
                || tier.MaximumQuantity < tier.MinimumQuantity
                || tier.UnitPrice is < 0 or > 1_000_000_000)
            {
                return false;
            }

            if (index + 1 < ordered.Count
                && (tier.MaximumQuantity is null || tier.MaximumQuantity >= ordered[index + 1].MinimumQuantity))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Tells whether <paramref name="tiers"/> are the two agreed quantity bands new and changed price sheets use.
    /// </summary>
    /// <param name="tiers">The price tiers.</param>
    public static bool StandardBands(IReadOnlyList<PriceTier> tiers)
    {
        return tiers.Count == 2
            && tiers.Any(tier => tier.MinimumQuantity == 1 && tier.MaximumQuantity == 9)
            && tiers.Any(tier => tier.MinimumQuantity == 10 && tier.MaximumQuantity is null);
    }
}
