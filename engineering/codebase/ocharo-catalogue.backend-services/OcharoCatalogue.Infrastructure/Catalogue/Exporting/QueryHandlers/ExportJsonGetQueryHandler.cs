using System.Text.Json;
using OcharoCatalogue.Application.Catalogue.Core.Repositories;
using OcharoCatalogue.Application.Catalogue.Core.Serializers;
using OcharoCatalogue.Application.Catalogue.Exporting.Queries;
using OcharoCatalogue.Domain.Catalogue.Models;
using OcharoCatalogue.Infrastructure.Catalogue.Settings.FoundationServices;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Infrastructure.Catalogue.Exporting.QueryHandlers;

/// <summary>Handles <see cref="ExportJsonGetQuery"/>.</summary>
public sealed class ExportJsonGetQueryHandler(
    ICatalogueRepository repository,
    CatalogueSettingsService settings,
    TimeProvider timeProvider) : IQueryHandler<ExportJsonGetQuery, AppResult<byte[]>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<byte[]>> HandleAsync(ExportJsonGetQuery request, CancellationToken ct)
    {
        var products = (await repository.GetProductsAsync(ct))
            .OrderBy(record => record.Id, StringComparer.Ordinal)
            .Select(record => new
            {
                revision = record.Revision,
                updatedAt = record.UpdatedAt,
                document = CatalogueJson.Read<ProductDocument>(record.Json),
            });
        var collections = (await repository.GetCollectionsAsync(ct))
            .Select(collection => CatalogueJson.Read<CollectionDocument>(collection.Json));
        var bundle = new
        {
            format = "ocharo-catalogue/1",
            exportedAt = timeProvider.GetUtcNow(),
            settings = await settings.GetAsync(ct),
            products,
            collections,
        };
        return AppResult<byte[]>.Ok(JsonSerializer.SerializeToUtf8Bytes(bundle, CatalogueJson.Indented));
    }
}
