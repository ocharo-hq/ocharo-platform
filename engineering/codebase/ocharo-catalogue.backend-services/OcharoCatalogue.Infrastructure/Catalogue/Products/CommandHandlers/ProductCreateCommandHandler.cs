using OcharoCatalogue.Application.Catalogue.Core.Errors;
using OcharoCatalogue.Application.Catalogue.Core.Repositories;
using OcharoCatalogue.Application.Catalogue.Products.Commands;
using OcharoCatalogue.Application.Catalogue.Products.Models;
using OcharoCatalogue.Infrastructure.Catalogue.Products.FoundationServices;
using OcharoCatalogue.Infrastructure.Catalogue.Products.Mappers;
using OcharoCatalogue.Infrastructure.Catalogue.Products.Models;
using OcharoCatalogue.Infrastructure.Catalogue.Settings.FoundationServices;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Infrastructure.Catalogue.Products.CommandHandlers;

/// <summary>Handles <see cref="ProductCreateCommand"/>.</summary>
public sealed class ProductCreateCommandHandler(
    ICatalogueRepository repository,
    CatalogueSettingsService settings,
    ProductDetailService details,
    TimeProvider timeProvider) : ICommandHandler<ProductCreateCommand, AppResult<ProductDetail>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<ProductDetail>> HandleAsync(ProductCreateCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var current = await settings.GetAsync(ct);
        var garmentType = input.GarmentType.Trim();
        var existing = await repository.GetProductsAsync(ct);
        var variant = string.IsNullOrWhiteSpace(input.VariantCode)
            ? ProductVariants.Next(existing, garmentType)
            : input.VariantCode.Trim();
        var id = string.IsNullOrWhiteSpace(input.Id) ? $"{garmentType}-{variant}" : input.Id.Trim();
        if (existing.Any(record => record.Id == id))
        {
            return AppResult<ProductDetail>.Fail(CatalogueErrors.Conflict($"A product with id '{id}' already exists."));
        }

        var document = ProductInputNormalizer.ToDocument(
            input with { VariantCode = variant },
            id,
            current,
            ProductCurrentParts.None);
        var now = timeProvider.GetUtcNow();
        var record = ProductProjections.ToRecord(
            document,
            1,
            now,
            now);
        await repository.InsertProductAsync(
            record,
            ProductHistoryLines.Summary(input.ChangeSummary, "Created"),
            ct);
        return AppResult<ProductDetail>.Ok(await details.GetAsync(record, ct));
    }
}
