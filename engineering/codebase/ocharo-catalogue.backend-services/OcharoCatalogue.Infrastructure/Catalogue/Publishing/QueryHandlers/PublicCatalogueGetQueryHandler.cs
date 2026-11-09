using OcharoCatalogue.Application.Catalogue.Core.Errors;
using OcharoCatalogue.Application.Catalogue.Core.Repositories;
using OcharoCatalogue.Application.Catalogue.Core.Serializers;
using OcharoCatalogue.Application.Catalogue.Publishing.Models;
using OcharoCatalogue.Application.Catalogue.Publishing.Queries;
using OcharoCatalogue.Domain.Catalogue.Models;
using OcharoCatalogue.Infrastructure.Catalogue.Core.Policies;
using OcharoCatalogue.Infrastructure.Catalogue.Publishing.Mappers;
using OcharoCatalogue.Infrastructure.Catalogue.Settings.FoundationServices;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Infrastructure.Catalogue.Publishing.QueryHandlers;

/// <summary>Handles <see cref="PublicCatalogueGetQuery"/>.</summary>
public sealed class PublicCatalogueGetQueryHandler(
    ICatalogueRepository repository,
    CatalogueSettingsService settings,
    TimeProvider timeProvider) : IQueryHandler<PublicCatalogueGetQuery, AppResult<PublicCatalogue>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<PublicCatalogue>> HandleAsync(
        PublicCatalogueGetQuery request,
        CancellationToken ct)
    {
        var code = request.Locale ?? "uz";
        if (!CatalogueLocales.Supported(code))
        {
            return AppResult<PublicCatalogue>.Fail(CatalogueErrors.Invalid("Unsupported catalogue language."));
        }

        var current = await settings.GetAsync(ct);
        var garments = current.GarmentTypes.ToDictionary(garment => garment.Key);
        var products = (await repository.GetProductsAsync(ct))
            .Where(record => record.Status == ProductStatus.Active)
            .OrderBy(record => record.SortOrder)
            .ThenBy(record => record.GarmentType)
            .ThenBy(record => record.Id, StringComparer.Ordinal)
            .Select(record => (Record: record, Document: CatalogueJson.Read<ProductDocument>(record.Json)))
            .Where(row => ProductPublicationEligibility.Eligible(row.Document, code))
            .Select(row => PublicProductMapper.ToPublic(
                row.Document,
                row.Record.Revision,
                row.Record.UpdatedAt,
                code,
                garments))
            .ToList();
        var active = products.Select(product => product.Id).ToHashSet();
        var collections = (await repository.GetCollectionsAsync(ct))
            .Select(collection => CatalogueJson.Read<CollectionDocument>(collection.Json))
            .Where(collection => collection.IsPublished)
            .OrderBy(collection => collection.SortOrder)
            .Select(collection => new PublicCollection(
                collection.Id,
                collection.Names.For(code),
                collection.Names,
                collection.Description,
                collection.ProductIds.Where(active.Contains).ToList()))
            .ToList();
        var feed = new PublicCatalogue(
            current.BrandName,
            current.Currency,
            code,
            timeProvider.GetUtcNow(),
            current.PriceNote,
            current.GarmentTypes,
            collections,
            products);
        return AppResult<PublicCatalogue>.Ok(feed);
    }
}
