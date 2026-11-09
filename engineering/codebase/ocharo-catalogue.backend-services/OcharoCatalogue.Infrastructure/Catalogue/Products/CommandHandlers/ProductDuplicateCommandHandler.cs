using OcharoCatalogue.Application.Catalogue.Core.Errors;
using OcharoCatalogue.Application.Catalogue.Core.Repositories;
using OcharoCatalogue.Application.Catalogue.Core.Serializers;
using OcharoCatalogue.Application.Catalogue.Products.Commands;
using OcharoCatalogue.Application.Catalogue.Products.Models;
using OcharoCatalogue.Domain.Catalogue.Models;
using OcharoCatalogue.Infrastructure.Catalogue.Products.FoundationServices;
using OcharoCatalogue.Infrastructure.Catalogue.Products.Mappers;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Infrastructure.Catalogue.Products.CommandHandlers;

/// <summary>Handles <see cref="ProductDuplicateCommand"/>.</summary>
public sealed class ProductDuplicateCommandHandler(
    ICatalogueRepository repository,
    ProductDetailService details,
    TimeProvider timeProvider) : ICommandHandler<ProductDuplicateCommand, AppResult<ProductDetail>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<ProductDetail>> HandleAsync(ProductDuplicateCommand request, CancellationToken ct)
    {
        var record = await repository.FindProductAsync(request.Id, ct);
        if (record is null)
        {
            return AppResult<ProductDetail>.Fail(CatalogueErrors.NotFound("Product"));
        }

        var source = CatalogueJson.Read<ProductDocument>(record.Json);
        var existing = await repository.GetProductsAsync(ct);
        var variant = ProductVariants.Next(existing, source.GarmentType);
        var additional = (source.Locales.Additional ?? new Dictionary<string, ProductLocale>())
            .ToDictionary(
                entry => entry.Key,
                entry => Draft(entry.Value),
                StringComparer.Ordinal);
        var copy = source with
        {
            Id = $"{source.GarmentType}-{variant}",
            VariantCode = variant,
            Status = ProductStatus.Draft,
            Issues = [],
            Source = null,
            SourceVerbatim = null,
            Locales = new ProductLocales(
                Draft(source.Locales.Uz),
                Draft(source.Locales.Ru),
                Draft(source.Locales.En))
            {
                Additional = additional,
            },
            Pricing = source.Pricing is null ? null : source.Pricing with { Status = PricingStatus.Draft },
        };
        var now = timeProvider.GetUtcNow();
        var created = ProductProjections.ToRecord(
            copy,
            1,
            now,
            now);
        await repository.InsertProductAsync(
            created,
            $"Duplicated from {source.Id}",
            ct);
        return AppResult<ProductDetail>.Ok(await details.GetAsync(created, ct));
    }

    private static ProductLocale Draft(ProductLocale locale)
    {
        return locale with { Status = LocaleStatus.Draft };
    }
}
