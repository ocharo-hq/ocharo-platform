using OcharoCatalogue.Application.Catalogue.Core.Errors;
using OcharoCatalogue.Application.Catalogue.Core.Repositories;
using OcharoCatalogue.Application.Catalogue.Core.Serializers;
using OcharoCatalogue.Application.Catalogue.Products.Commands;
using OcharoCatalogue.Application.Catalogue.Products.Models;
using OcharoCatalogue.Domain.Catalogue.Models;
using OcharoCatalogue.Infrastructure.Catalogue.Products.FoundationServices;
using OcharoCatalogue.Infrastructure.Catalogue.Products.Mappers;
using OcharoCatalogue.Infrastructure.Catalogue.Products.Models;
using OcharoCatalogue.Infrastructure.Catalogue.Settings.FoundationServices;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Infrastructure.Catalogue.Products.CommandHandlers;

/// <summary>Handles <see cref="ProductUpdateCommand"/>.</summary>
public sealed class ProductUpdateCommandHandler(
    ICatalogueRepository repository,
    CatalogueSettingsService settings,
    ProductDetailService details,
    TimeProvider timeProvider) : ICommandHandler<ProductUpdateCommand, AppResult<ProductDetail>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<ProductDetail>> HandleAsync(ProductUpdateCommand request, CancellationToken ct)
    {
        var input = request.Input;
        if (input.ExpectedRevision is not { } expected)
        {
            return AppResult<ProductDetail>.Fail(
                CatalogueErrors.Invalid("Send the revision you edited (expectedRevision)."));
        }

        var record = await repository.FindProductAsync(request.Id, ct);
        if (record is null)
        {
            return AppResult<ProductDetail>.Fail(CatalogueErrors.NotFound("Product"));
        }

        var current = CatalogueJson.Read<ProductDocument>(record.Json);
        var parts = new ProductCurrentParts(
            current.Media,
            current.SourceVerbatim,
            current.Source,
            current.Locales,
            current.Pricing);
        var document = ProductInputNormalizer.ToDocument(
            input with { VariantCode = input.VariantCode ?? current.VariantCode },
            request.Id,
            await settings.GetAsync(ct),
            parts);
        var next = ProductProjections.ToRecord(
            document,
            expected + 1,
            record.CreatedAt,
            timeProvider.GetUtcNow());
        var saved = await repository.UpdateProductAsync(
            next,
            expected,
            ProductHistoryLines.Summary(input.ChangeSummary, ProductHistoryLines.Describe(current, document)),
            ct);
        return AppResult<ProductDetail>.Ok(await details.GetAsync(saved, ct));
    }
}
