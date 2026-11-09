using OcharoCatalogue.Application.Catalogue.Core.Errors;
using OcharoCatalogue.Application.Catalogue.Core.Repositories;
using OcharoCatalogue.Application.Catalogue.Core.Serializers;
using OcharoCatalogue.Application.Catalogue.Products.Queries;
using OcharoCatalogue.Domain.Catalogue.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Infrastructure.Catalogue.Products.QueryHandlers;

/// <summary>Handles <see cref="ProductRevisionGetByIdQuery"/>.</summary>
public sealed class ProductRevisionGetByIdQueryHandler(ICatalogueRepository repository)
    : IQueryHandler<ProductRevisionGetByIdQuery, AppResult<ProductDocument>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<ProductDocument>> HandleAsync(
        ProductRevisionGetByIdQuery request,
        CancellationToken ct)
    {
        var snapshot = await repository.FindRevisionAsync(
            request.Id,
            request.Revision,
            ct);
        if (snapshot is null)
        {
            return AppResult<ProductDocument>.Fail(CatalogueErrors.NotFound("Revision"));
        }

        return AppResult<ProductDocument>.Ok(CatalogueJson.Read<ProductDocument>(snapshot.Json));
    }
}
