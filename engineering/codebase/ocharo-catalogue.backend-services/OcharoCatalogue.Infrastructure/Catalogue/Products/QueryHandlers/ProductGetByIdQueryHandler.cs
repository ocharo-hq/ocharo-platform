using OcharoCatalogue.Application.Catalogue.Core.Errors;
using OcharoCatalogue.Application.Catalogue.Core.Repositories;
using OcharoCatalogue.Application.Catalogue.Products.Models;
using OcharoCatalogue.Application.Catalogue.Products.Queries;
using OcharoCatalogue.Infrastructure.Catalogue.Products.FoundationServices;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Infrastructure.Catalogue.Products.QueryHandlers;

/// <summary>Handles <see cref="ProductGetByIdQuery"/>.</summary>
public sealed class ProductGetByIdQueryHandler(ICatalogueRepository repository, ProductDetailService details)
    : IQueryHandler<ProductGetByIdQuery, AppResult<ProductDetail>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<ProductDetail>> HandleAsync(ProductGetByIdQuery request, CancellationToken ct)
    {
        var record = await repository.FindProductAsync(request.Id, ct);
        if (record is null)
        {
            return AppResult<ProductDetail>.Fail(CatalogueErrors.NotFound("Product"));
        }

        return AppResult<ProductDetail>.Ok(await details.GetAsync(record, ct));
    }
}
