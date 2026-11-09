using OcharoCatalogue.Application.Catalogue.Core.Errors;
using OcharoCatalogue.Application.Catalogue.Publishing.Models;
using OcharoCatalogue.Domain.Catalogue.Models;
using OcharoCatalogue.Infrastructure.Catalogue.Core.Mappers;
using OcharoCatalogue.Infrastructure.Catalogue.Core.Policies;

namespace OcharoCatalogue.Infrastructure.Catalogue.Publishing.Mappers;

/// <summary>Maps a product approved for a language to the public view feeds and releases carry.</summary>
public static class PublicProductMapper
{
    /// <summary>Maps <paramref name="document"/> to its public view in <paramref name="locale"/>.</summary>
    /// <param name="document">The product document.</param>
    /// <param name="revision">The product's revision.</param>
    /// <param name="updatedAt">When the revision was saved.</param>
    /// <param name="locale">The view's language.</param>
    /// <param name="garments">The garment families by key.</param>
    public static PublicProduct ToPublic(
        ProductDocument document,
        int revision,
        DateTimeOffset updatedAt,
        string locale,
        IReadOnlyDictionary<string, GarmentTypeDefinition> garments)
    {
        if (!ProductPublicationEligibility.Eligible(document, locale))
        {
            throw CatalogueErrors.Invalid("Product is not approved for this catalogue language.").ToException();
        }

        var copy = document.Locales.For(locale);
        var garment = (locale is "uz" or "ru" or "en") && garments.TryGetValue(document.GarmentType, out var definition)
            ? definition.For(locale)
            : document.GarmentType;
        var images = ProductPublicationEligibility.ActiveImages(document)
            .Select(image => new PublicImage(
                image.Role,
                CatalogueUrls.Media(image.Sha256),
                image.HasPreview ? CatalogueUrls.Preview(image.Sha256) : null,
                image.Width,
                image.Height,
                image.Alt ?? $"{copy.Name ?? garment} — {image.Role}",
                image.Sha256,
                ProductPublicationEligibility.MediaGroup(image)))
            .ToList();
        PublicPricing? pricing = null;
        if (ProductPublicationEligibility.PublicPricing(document.Pricing))
        {
            pricing = new PublicPricing(
                document.Pricing!.Currency,
                document.Pricing.Status,
                document.Pricing.RetailPrice,
                document.Pricing.WholesalePrice,
                document.Pricing.Tiers,
                document.Pricing.Wording);
        }

        return new PublicProduct(
            document.Id,
            document.GarmentType,
            garment,
            document.VariantCode,
            copy.Name ?? garment,
            copy.Fabric,
            copy.Decoration,
            copy.Description,
            copy.Status,
            ApprovedLocales(document.Locales),
            document.Sizes,
            pricing,
            images,
            document.Tags,
            revision,
            updatedAt,
            document.Availability,
            document.AvailableSizes);
    }

    private static ProductLocales ApprovedLocales(ProductLocales source)
    {
        var additional = CatalogueLocales.All
            .Where(code => code is not ("uz" or "ru" or "en"))
            .Where(code => IsApproved(source.For(code)))
            .ToDictionary(
                code => code,
                code => source.For(code),
                StringComparer.Ordinal);
        return new ProductLocales(
            Approved(source.For("uz")),
            Approved(source.For("ru")),
            Approved(source.For("en")))
        {
            Additional = additional,
        };
    }

    private static ProductLocale Approved(ProductLocale locale)
    {
        return IsApproved(locale) ? locale : ProductLocale.Empty();
    }

    private static bool IsApproved(ProductLocale locale)
    {
        return locale.Status == LocaleStatus.Approved && !string.IsNullOrWhiteSpace(locale.Name);
    }
}
