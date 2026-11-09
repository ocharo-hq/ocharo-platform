using OcharoCatalogue.Application.Catalogue.Core.Errors;
using OcharoCatalogue.Application.Catalogue.Core.Repositories;
using OcharoCatalogue.Application.Catalogue.Core.Serializers;
using OcharoCatalogue.Application.Catalogue.Products.Models;
using OcharoCatalogue.Application.Catalogue.Products.Queries;
using OcharoCatalogue.Domain.Catalogue.Models;
using OcharoCatalogue.Infrastructure.Catalogue.Products.Mappers;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Infrastructure.Catalogue.Products.QueryHandlers;

/// <summary>Handles <see cref="ProductGetAllQuery"/>.</summary>
public sealed class ProductGetAllQueryHandler(ICatalogueRepository repository)
    : IQueryHandler<ProductGetAllQuery, AppResult<IReadOnlyList<ProductSummary>>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<IReadOnlyList<ProductSummary>>> HandleAsync(
        ProductGetAllQuery request,
        CancellationToken ct)
    {
        var filter = request.Filter;
        var records = await repository.GetProductsAsync(ct);
        var collections = (await repository.GetCollectionsAsync(ct))
            .Select(record => CatalogueJson.Read<CollectionDocument>(record.Json))
            .ToList();
        HashSet<string>? inCollection = null;
        if (!string.IsNullOrWhiteSpace(filter.Collection))
        {
            var collection = await repository.FindCollectionAsync(filter.Collection, ct);
            if (collection is null)
            {
                return AppResult<IReadOnlyList<ProductSummary>>.Fail(CatalogueErrors.NotFound("Collection"));
            }

            inCollection = CatalogueJson.Read<CollectionDocument>(collection.Json).ProductIds.ToHashSet();
        }

        var query = filter.Query?.Trim();
        var rows = records
            .Select(record => (Record: record, Document: CatalogueJson.Read<ProductDocument>(record.Json)))
            .Where(row => string.IsNullOrWhiteSpace(filter.GarmentType) || row.Record.GarmentType == filter.GarmentType)
            .Where(row => string.IsNullOrWhiteSpace(filter.Status) || row.Record.Status == filter.Status)
            .Where(row => inCollection is null || inCollection.Contains(row.Record.Id))
            .Where(row => string.IsNullOrWhiteSpace(filter.Tag) || row.Document.Tags.Contains(filter.Tag))
            .Where(row => string.IsNullOrEmpty(query) || Matches(row.Document, query))
            .OrderBy(row => row.Record.SortOrder)
            .ThenBy(row => row.Record.GarmentType)
            .ThenBy(row => row.Record.Id, StringComparer.Ordinal)
            .Select(row => ProductProjections.ToSummary(
                row.Document,
                row.Record,
                CollectionIds(collections, row.Record.Id)))
            .ToList();
        return AppResult<IReadOnlyList<ProductSummary>>.Ok(rows);
    }

    private static List<string> CollectionIds(IReadOnlyList<CollectionDocument> collections, string productId)
    {
        return collections
            .Where(collection => collection.ProductIds.Contains(productId))
            .Select(collection => collection.Id)
            .ToList();
    }

    private static bool Matches(ProductDocument document, string query)
    {
        var copy = CatalogueLocales.All
            .SelectMany(code => new[] { document.Locales.For(code).Name, document.Locales.For(code).Fabric });
        return new[] { document.Id, document.Notes }
            .Concat(copy)
            .Concat(document.Tags)
            .Any(value => value?.Contains(query, StringComparison.OrdinalIgnoreCase) == true);
    }
}
