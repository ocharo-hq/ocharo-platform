using OcharoCatalogue.Application.Catalogue.Core.Repositories;
using OcharoCatalogue.Application.Catalogue.Core.Serializers;
using OcharoCatalogue.Application.Catalogue.Products.Models;
using OcharoCatalogue.Domain.Catalogue.Entities;
using OcharoCatalogue.Domain.Catalogue.Models;
using OcharoCatalogue.Infrastructure.Catalogue.Core.Mappers;

namespace OcharoCatalogue.Infrastructure.Catalogue.Products.FoundationServices;

/// <summary>
/// Provides the full view of a stored product: its document, media URLs and the collections holding it.
/// </summary>
public sealed class ProductDetailService(ICatalogueRepository repository)
{
    /// <summary>Gets the full view of <paramref name="record"/>.</summary>
    /// <param name="record">The product row.</param>
    /// <param name="ct">The token that cancels the read.</param>
    public async Task<ProductDetail> GetAsync(ProductRecord record, CancellationToken ct)
    {
        var document = CatalogueJson.Read<ProductDocument>(record.Json);
        var collections = (await repository.GetCollectionsAsync(ct))
            .Where(collection => CatalogueJson.Read<CollectionDocument>(collection.Json).ProductIds.Contains(record.Id))
            .Select(collection => collection.Id)
            .ToList();
        return ToDetail(
            document,
            record,
            collections);
    }

    /// <summary>Maps a product to its full view, given the collections holding it.</summary>
    /// <param name="document">The product document.</param>
    /// <param name="record">The product row.</param>
    /// <param name="collectionIds">The collections holding the product.</param>
    public static ProductDetail ToDetail(
        ProductDocument document,
        ProductRecord record,
        IReadOnlyList<string> collectionIds)
    {
        var views = document.Media
            .Select(media => new MediaView(
                media,
                CatalogueUrls.Media(media.Sha256),
                media.HasPreview ? CatalogueUrls.Preview(media.Sha256) : null))
            .ToList();
        return new ProductDetail(
            document,
            record.Revision,
            record.CreatedAt,
            record.UpdatedAt,
            views,
            collectionIds);
    }
}
