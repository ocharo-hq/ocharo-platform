using OcharoCatalogue.Application.Catalogue.Core.Repositories;
using OcharoCatalogue.Application.Catalogue.Core.Serializers;
using OcharoCatalogue.Application.Catalogue.Designs.Models;
using OcharoCatalogue.Application.Catalogue.Designs.Queries;
using OcharoCatalogue.Application.Catalogue.Products.Models;
using OcharoCatalogue.Domain.Catalogue.Entities;
using OcharoCatalogue.Domain.Catalogue.Models;
using OcharoCatalogue.Infrastructure.Catalogue.Products.FoundationServices;
using OcharoCatalogue.Infrastructure.Catalogue.Settings.FoundationServices;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Infrastructure.Catalogue.Designs.QueryHandlers;

/// <summary>Handles <see cref="DesignSourceGetQuery"/>.</summary>
public sealed class DesignSourceGetQueryHandler(ICatalogueRepository repository, CatalogueSettingsService settings)
    : IQueryHandler<DesignSourceGetQuery, AppResult<DesignSource>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<DesignSource>> HandleAsync(DesignSourceGetQuery request, CancellationToken ct)
    {
        var rows = await repository.GetProductsAsync(ct);
        var collections = (await repository.GetCollectionsAsync(ct))
            .Select(collection => CatalogueJson.Read<CollectionDocument>(collection.Json))
            .ToArray();
        var products = rows
            .OrderBy(row => row.SortOrder)
            .ThenBy(row => row.Id, StringComparer.Ordinal)
            .Select(row => ToDetail(row, collections))
            .ToArray();
        return AppResult<DesignSource>.Ok(new DesignSource(products, await settings.GetAsync(ct)));
    }

    private static ProductDetail ToDetail(ProductRecord row, IReadOnlyList<CollectionDocument> collections)
    {
        var document = CatalogueJson.Read<ProductDocument>(row.Json);
        var collectionIds = collections
            .Where(collection => collection.ProductIds.Contains(document.Id))
            .Select(collection => collection.Id)
            .ToArray();
        return ProductDetailService.ToDetail(
            document,
            row,
            collectionIds);
    }
}
