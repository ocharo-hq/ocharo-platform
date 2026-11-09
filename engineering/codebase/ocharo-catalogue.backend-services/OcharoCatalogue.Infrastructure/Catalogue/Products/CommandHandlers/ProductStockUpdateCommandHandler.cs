using OcharoCatalogue.Application.Catalogue.Core.Errors;
using OcharoCatalogue.Application.Catalogue.Core.Repositories;
using OcharoCatalogue.Application.Catalogue.Core.Serializers;
using OcharoCatalogue.Application.Catalogue.Products.Commands;
using OcharoCatalogue.Application.Catalogue.Products.Models;
using OcharoCatalogue.Domain.Catalogue.Models;
using OcharoCatalogue.Infrastructure.Catalogue.Products.Mappers;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Infrastructure.Catalogue.Products.CommandHandlers;

/// <summary>Handles <see cref="ProductStockUpdateCommand"/>.</summary>
public sealed class ProductStockUpdateCommandHandler(ICatalogueRepository repository, TimeProvider timeProvider)
    : ICommandHandler<ProductStockUpdateCommand, AppResult<StockRow>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<StockRow>> HandleAsync(ProductStockUpdateCommand request, CancellationToken ct)
    {
        var input = request.Input;
        var record = await repository.FindProductAsync(request.Id, ct);
        if (record is null)
        {
            return AppResult<StockRow>.Fail(CatalogueErrors.NotFound("Product"));
        }

        var current = CatalogueJson.Read<ProductDocument>(record.Json);
        var next = current with
        {
            Stock = ProductInputNormalizer.Stock(input.Stock, current.Sizes),
            MadeToOrder = input.MadeToOrder,
        };
        var saved = await repository.UpdateProductAsync(
            ProductProjections.ToRecord(
                next,
                input.ExpectedRevision + 1,
                record.CreatedAt,
                timeProvider.GetUtcNow()),
            input.ExpectedRevision,
            Summary(next),
            ct);
        return AppResult<StockRow>.Ok(ProductProjections.ToStockRow(
            next,
            saved.Status,
            saved.Revision));
    }

    private static string Summary(ProductDocument document)
    {
        if (document.MadeToOrder)
        {
            return "Stock: made to order";
        }

        return document.Stock is null ? "Stock: not tracked" : $"Stock: {document.Stock.Values.Sum()} pieces";
    }
}
