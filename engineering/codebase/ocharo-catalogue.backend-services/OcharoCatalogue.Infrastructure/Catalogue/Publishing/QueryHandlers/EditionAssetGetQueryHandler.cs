using OcharoCatalogue.Application.Catalogue.Core.Errors;
using OcharoCatalogue.Application.Catalogue.Core.Repositories;
using OcharoCatalogue.Application.Catalogue.Core.Serializers;
using OcharoCatalogue.Application.Catalogue.Publishing.Models;
using OcharoCatalogue.Application.Catalogue.Publishing.Queries;
using OcharoCatalogue.Application.Catalogue.Publishing.Services;
using OcharoCatalogue.Infrastructure.Catalogue.Publishing.Mappers;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Infrastructure.Catalogue.Publishing.QueryHandlers;

/// <summary>Handles <see cref="EditionAssetGetQuery"/>.</summary>
public sealed class EditionAssetGetQueryHandler(IDesignRepository designs, IReleaseAssetStore releaseAssets)
    : IQueryHandler<EditionAssetGetQuery, AppResult<CataloguePublicAsset>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<CataloguePublicAsset>> HandleAsync(
        EditionAssetGetQuery request,
        CancellationToken ct)
    {
        var record = await designs.PublishedAsync(
            request.Slug,
            request.Locale,
            request.ReleaseId,
            ct);
        if (record is null)
        {
            return AppResult<CataloguePublicAsset>.Fail(CatalogueErrors.NotFound("Published catalogue"));
        }

        var release = CatalogueJson.Read<CataloguePreparedRelease>(record.Json);
        if (!release.Assets.TryGetValue(request.Asset, out var hash))
        {
            return AppResult<CataloguePublicAsset>.Fail(CatalogueErrors.NotFound("Catalogue artifact"));
        }

        var name = ReleaseDownloadNames.For(release, request.Asset);
        if (await releaseAssets.LinkAsync(hash, ct) is { } link)
        {
            var contentType = Path.GetExtension(request.Asset) == ".pdf" ? "application/pdf" : "image/png";
            return AppResult<CataloguePublicAsset>.Ok(new CataloguePublicAsset(
                name,
                hash,
                contentType,
                null,
                link));
        }

        var stored = await designs.AssetAsync(hash, ct);
        if (stored is null)
        {
            return AppResult<CataloguePublicAsset>.Fail(CatalogueErrors.NotFound("Catalogue artifact"));
        }

        return AppResult<CataloguePublicAsset>.Ok(new CataloguePublicAsset(
            name,
            stored.Sha256,
            stored.ContentType,
            stored.Bytes));
    }
}
