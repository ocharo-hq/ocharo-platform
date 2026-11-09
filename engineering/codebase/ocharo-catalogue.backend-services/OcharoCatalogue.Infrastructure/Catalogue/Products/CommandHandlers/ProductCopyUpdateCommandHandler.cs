using OcharoCatalogue.Application.Catalogue.Core.Errors;
using OcharoCatalogue.Application.Catalogue.Core.Repositories;
using OcharoCatalogue.Application.Catalogue.Core.Serializers;
using OcharoCatalogue.Application.Catalogue.Products.Commands;
using OcharoCatalogue.Application.Catalogue.Products.Models;
using OcharoCatalogue.Domain.Catalogue.Models;
using OcharoCatalogue.Infrastructure.Catalogue.Products.Mappers;
using OcharoCatalogue.Infrastructure.Catalogue.Products.Policies;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Infrastructure.Catalogue.Products.CommandHandlers;

/// <summary>Handles <see cref="ProductCopyUpdateCommand"/>.</summary>
public sealed class ProductCopyUpdateCommandHandler(ICatalogueRepository repository, TimeProvider timeProvider)
    : ICommandHandler<ProductCopyUpdateCommand, AppResult<ProductCopyRow>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<ProductCopyRow>> HandleAsync(
        ProductCopyUpdateCommand request,
        CancellationToken ct)
    {
        var input = request.Input;
        var record = await repository.FindProductAsync(request.Id, ct);
        if (record is null)
        {
            return AppResult<ProductCopyRow>.Fail(CatalogueErrors.NotFound("Product"));
        }

        var current = CatalogueJson.Read<ProductDocument>(record.Json);
        var locales = ProductInputNormalizer.Locales(input.Locales, current.Locales);
        var next = current with { Locales = locales };
        if (ProductActivationPolicy.Problem(next) is { } problem)
        {
            return AppResult<ProductCopyRow>.Fail(
                CatalogueErrors.Invalid($"Cannot keep the product active: {problem}."));
        }

        var approved = CatalogueLocales.All.Count(code => locales.For(code).Status == LocaleStatus.Approved);
        var saved = await repository.UpdateProductAsync(
            ProductProjections.ToRecord(
                next,
                input.ExpectedRevision + 1,
                record.CreatedAt,
                timeProvider.GetUtcNow()),
            input.ExpectedRevision,
            $"Edited copy ({approved}/{CatalogueLocales.All.Count} approved)",
            ct);
        return AppResult<ProductCopyRow>.Ok(ProductProjections.ToCopyRow(next, saved));
    }
}
