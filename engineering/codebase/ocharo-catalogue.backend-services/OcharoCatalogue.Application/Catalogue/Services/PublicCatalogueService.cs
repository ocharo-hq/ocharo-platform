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
        var code = locale is "ru" or "en" ? locale : "uz";
        var current = await settings.GetAsync(ct);
        var garments = current.GarmentTypes.ToDictionary(g => g.Key);
        var products = (await repository.GetProductsAsync(ct))
            .Where(r => r.Status == ProductStatus.Active)
            .OrderBy(r => r.SortOrder).ThenBy(r => r.GarmentType).ThenBy(r => r.Id, StringComparer.Ordinal)
            .Select(r => (Record: r, Document: CatalogueJson.Read<ProductDocument>(r.Json)))
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
        var copy = d.Locales.For(locale);
        var garment = garments.TryGetValue(d.GarmentType, out var g) ? g.For(locale) : d.GarmentType;
        var images = d.Media
            .OrderBy(m => m.Role == MediaRole.Cutout ? 0 : m.Role == MediaRole.Model ? 1 : 2)
            .Select(m => new PublicImage(m.Role, CatalogueUrls.Media(m.Sha256), m.HasPreview ? CatalogueUrls.Preview(m.Sha256) : null,
                m.Width, m.Height, m.Alt ?? $"{copy.Name ?? garment} — {m.Role}", m.Sha256))
            .ToList();
        var pricing = d.Pricing is null
            ? null
            : new PublicPricing(d.Pricing.Currency, d.Pricing.Status, d.Pricing.RetailPrice, d.Pricing.WholesalePrice, d.Pricing.Tiers, d.Pricing.Wording);
        return new PublicProduct(d.Id, d.GarmentType, garment, d.VariantCode, copy.Name ?? garment, copy.Fabric, copy.Decoration,
            copy.Description, copy.Status, d.Locales, d.Sizes, pricing, images, d.Tags, revision, updatedAt, d.Availability, d.AvailableSizes);
    }
}
