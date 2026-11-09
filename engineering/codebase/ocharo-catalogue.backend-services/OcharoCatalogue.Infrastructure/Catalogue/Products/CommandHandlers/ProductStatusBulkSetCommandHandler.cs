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

/// <summary>Handles <see cref="ProductStatusBulkSetCommand"/>.</summary>
public sealed class ProductStatusBulkSetCommandHandler(ICatalogueRepository repository, TimeProvider timeProvider)
    : ICommandHandler<ProductStatusBulkSetCommand, AppResult<BulkResult>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<BulkResult>> HandleAsync(ProductStatusBulkSetCommand request, CancellationToken ct)
    {
        var input = request.Input;
        if (!ProductStatus.All.Contains(input.Status))
        {
            return AppResult<BulkResult>.Fail(CatalogueErrors.Invalid("Unknown product status."));
        }

        var skipped = new List<string>();
        var changed = 0;
        foreach (var id in input.ProductIds.Distinct())
        {
            var record = await repository.FindProductAsync(id, ct);
            if (record is null)
            {
                skipped.Add($"{id}: not found");
                continue;
            }

            var document = CatalogueJson.Read<ProductDocument>(record.Json);
            if (document.Status == input.Status)
            {
                continue;
            }

            var next = document with { Status = input.Status };
            if (ProductActivationPolicy.Problem(next) is { } problem)
            {
                skipped.Add($"{id}: {problem}");
                continue;
            }

            await repository.UpdateProductAsync(
                ProductProjections.ToRecord(
                    next,
                    record.Revision + 1,
                    record.CreatedAt,
                    timeProvider.GetUtcNow()),
                record.Revision,
                $"Status {document.Status} → {input.Status}",
                ct);
            changed++;
        }

        return AppResult<BulkResult>.Ok(new BulkResult(changed, skipped));
    }
}
