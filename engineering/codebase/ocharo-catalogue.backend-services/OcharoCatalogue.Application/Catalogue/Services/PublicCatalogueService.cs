using OcharoCatalogue.Application.Abstractions;
using OcharoCatalogue.Application.Catalogue.Models;
using OcharoCatalogue.Application.Catalogue.Repositories;
using OcharoCatalogue.Domain.Catalogue.Models;

namespace OcharoCatalogue.Application.Catalogue.Services;

/// <summary>Builds the read-only feed other Ocharo platforms consume: active products and published collections only.</summary>
public sealed class PublicCatalogueService(ICatalogueRepository repository, SettingsService settings, IClock clock)
{
    public async Task<PublicCatalogue> BuildAsync(string? locale, CancellationToken ct)
    {
        var code = locale ?? "uz";
        if (!CatalogueLocales.Supported(code)) throw new CatalogueException("Unsupported catalogue language.");
        var current = await settings.GetAsync(ct);
        var garments = current.GarmentTypes.ToDictionary(g => g.Key);
        var products = (await repository.GetProductsAsync(ct))
            .Where(r => r.Status == ProductStatus.Active)
            .OrderBy(r => r.SortOrder).ThenBy(r => r.GarmentType).ThenBy(r => r.Id, StringComparer.Ordinal)
            .Select(r => (Record: r, Document: CatalogueJson.Read<ProductDocument>(r.Json)))
            .Where(x => ProductPublicationEligibility.Eligible(x.Document, code))
            .Select(x => ToPublic(x.Document, x.Record.Revision, x.Record.UpdatedAt, code, garments))
            .ToList();
        var active = products.Select(p => p.Id).ToHashSet();
        var collections = (await repository.GetCollectionsAsync(ct))
            .Select(c => CatalogueJson.Read<CollectionDocument>(c.Json))
            .Where(c => c.IsPublished)
            .OrderBy(c => c.SortOrder)
            .Select(c => new PublicCollection(c.Id, c.Names.For(code), c.Names, c.Description, c.ProductIds.Where(active.Contains).ToList()))
            .ToList();
        return new PublicCatalogue(current.BrandName, current.Currency, code, clock.UtcNow, current.PriceNote, current.GarmentTypes, collections, products);
    }

    public static PublicProduct ToPublic(ProductDocument d, int revision, DateTimeOffset updatedAt, string locale,
        IReadOnlyDictionary<string, GarmentTypeDefinition> garments)
    {
        if (!ProductPublicationEligibility.Eligible(d, locale))
            throw new CatalogueException("Product is not approved for this catalogue language.");
        var copy = d.Locales.For(locale);
        var garment = (locale is "uz" or "ru" or "en") && garments.TryGetValue(d.GarmentType, out var g) ? g.For(locale) : d.GarmentType;
        var images = ProductPublicationEligibility.ActiveImages(d)
            .Select(m => new PublicImage(m.Role, CatalogueUrls.Media(m.Sha256), m.HasPreview ? CatalogueUrls.Preview(m.Sha256) : null,
                m.Width, m.Height, m.Alt ?? $"{copy.Name ?? garment} — {m.Role}", m.Sha256, ProductPublicationEligibility.MediaGroup(m)))
            .ToList();
        var pricing = !ProductPublicationEligibility.PublicPricing(d.Pricing)
            ? null
            : new PublicPricing(d.Pricing!.Currency, d.Pricing.Status, d.Pricing.RetailPrice, d.Pricing.WholesalePrice, d.Pricing.Tiers, d.Pricing.Wording);
        return new PublicProduct(d.Id, d.GarmentType, garment, d.VariantCode, copy.Name ?? garment, copy.Fabric, copy.Decoration,
            copy.Description, copy.Status, ApprovedLocales(d.Locales), d.Sizes, pricing, images, d.Tags, revision, updatedAt, d.Availability, d.AvailableSizes);
    }
    private static ProductLocales ApprovedLocales(ProductLocales source)
    {
        static ProductLocale Approved(ProductLocale locale) => locale.Status == LocaleStatus.Approved && !string.IsNullOrWhiteSpace(locale.Name) ? locale : ProductLocale.Empty();
        return new ProductLocales(Approved(source.For("uz")), Approved(source.For("ru")), Approved(source.For("en")))
        {
            Additional = CatalogueLocales.All.Where(code => code is not ("uz" or "ru" or "en"))
                .Where(code => source.For(code).Status == LocaleStatus.Approved && !string.IsNullOrWhiteSpace(source.For(code).Name))
                .ToDictionary(code => code, code => source.For(code), StringComparer.Ordinal),
        };
    }
}
