using OcharoCatalogue.Application.Catalogue.Core.Errors;
using OcharoCatalogue.Application.Catalogue.Core.Repositories;
using OcharoCatalogue.Application.Catalogue.Core.Serializers;
using OcharoCatalogue.Application.Catalogue.Products.Commands;
using OcharoCatalogue.Application.Catalogue.Products.Models;
using OcharoCatalogue.Domain.Catalogue.Models;
using OcharoCatalogue.Infrastructure.Catalogue.Products.FoundationServices;
using OcharoCatalogue.Infrastructure.Catalogue.Products.Mappers;
using OcharoCatalogue.Infrastructure.Catalogue.Products.Policies;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Infrastructure.Catalogue.Products.CommandHandlers;

/// <summary>Handles <see cref="ProductRestoreCommand"/>.</summary>
public sealed class ProductRestoreCommandHandler(
    ICatalogueRepository repository,
    ProductDetailService details,
    TimeProvider timeProvider) : ICommandHandler<ProductRestoreCommand, AppResult<ProductDetail>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<ProductDetail>> HandleAsync(ProductRestoreCommand request, CancellationToken ct)
    {
        var record = await repository.FindProductAsync(request.Id, ct);
        if (record is null)
        {
            return AppResult<ProductDetail>.Fail(CatalogueErrors.NotFound("Product"));
        }

        var snapshot = await repository.FindRevisionAsync(
            request.Id,
            request.Revision,
            ct);
        if (snapshot is null)
        {
            return AppResult<ProductDetail>.Fail(CatalogueErrors.NotFound("Revision"));
        }

        var document = CatalogueJson.Read<ProductDocument>(snapshot.Json);
        if (ProductActivationPolicy.Problem(document) is { } problem)
        {
            return AppResult<ProductDetail>.Fail(
                CatalogueErrors.Invalid($"Cannot restore an active product: {problem}."));
        }

        var next = ProductProjections.ToRecord(
            document,
            request.ExpectedRevision + 1,
            record.CreatedAt,
            timeProvider.GetUtcNow());
        var saved = await repository.UpdateProductAsync(
            next,
            request.ExpectedRevision,
            $"Restored revision {request.Revision}",
            ct);
        return AppResult<ProductDetail>.Ok(await details.GetAsync(saved, ct));
    }
}
