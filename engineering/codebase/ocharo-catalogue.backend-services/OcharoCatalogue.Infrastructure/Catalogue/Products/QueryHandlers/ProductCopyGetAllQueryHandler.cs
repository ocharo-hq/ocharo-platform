using OcharoCatalogue.Application.Catalogue.Core.Repositories;
using OcharoCatalogue.Application.Catalogue.Core.Serializers;
using OcharoCatalogue.Application.Catalogue.Products.Models;
using OcharoCatalogue.Application.Catalogue.Products.Queries;
using OcharoCatalogue.Domain.Catalogue.Models;
using OcharoCatalogue.Infrastructure.Catalogue.Products.Mappers;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Infrastructure.Catalogue.Products.QueryHandlers;

/// <summary>Handles <see cref="ProductCopyGetAllQuery"/>.</summary>
public sealed class ProductCopyGetAllQueryHandler(ICatalogueRepository repository)
    : IQueryHandler<ProductCopyGetAllQuery, AppResult<IReadOnlyList<ProductCopyRow>>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<IReadOnlyList<ProductCopyRow>>> HandleAsync(
        ProductCopyGetAllQuery request,
        CancellationToken ct)
    {
        var rows = (await repository.GetProductsAsync(ct))
            .OrderBy(record => record.SortOrder)
            .ThenBy(record => record.Id, StringComparer.Ordinal)
            .Select(record => ProductProjections.ToCopyRow(CatalogueJson.Read<ProductDocument>(record.Json), record))
            .ToList();
        return AppResult<IReadOnlyList<ProductCopyRow>>.Ok(rows);
    }
}
