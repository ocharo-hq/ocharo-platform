using OcharoCatalogue.Application.Catalogue.Core.Repositories;
using OcharoCatalogue.Application.Catalogue.Core.Serializers;
using OcharoCatalogue.Application.Catalogue.Products.Models;
using OcharoCatalogue.Application.Catalogue.Products.Queries;
using OcharoCatalogue.Domain.Catalogue.Models;
using OcharoCatalogue.Infrastructure.Catalogue.Products.Mappers;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Infrastructure.Catalogue.Products.QueryHandlers;

/// <summary>Handles <see cref="ProductStockGetAllQuery"/>.</summary>
public sealed class ProductStockGetAllQueryHandler(ICatalogueRepository repository)
    : IQueryHandler<ProductStockGetAllQuery, AppResult<IReadOnlyList<StockRow>>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<IReadOnlyList<StockRow>>> HandleAsync(
        ProductStockGetAllQuery request,
        CancellationToken ct)
    {
        var rows = (await repository.GetProductsAsync(ct))
            .OrderBy(record => record.SortOrder)
            .ThenBy(record => record.Id, StringComparer.Ordinal)
            .Select(record => ProductProjections.ToStockRow(
                CatalogueJson.Read<ProductDocument>(record.Json),
                record.Status,
                record.Revision))
            .ToList();
        return AppResult<IReadOnlyList<StockRow>>.Ok(rows);
    }
}
