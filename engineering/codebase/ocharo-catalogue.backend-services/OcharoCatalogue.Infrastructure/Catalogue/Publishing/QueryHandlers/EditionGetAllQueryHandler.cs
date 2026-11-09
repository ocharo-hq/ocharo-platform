using OcharoCatalogue.Application.Catalogue.Core.Errors;
using OcharoCatalogue.Application.Catalogue.Core.Repositories;
using OcharoCatalogue.Application.Catalogue.Core.Serializers;
using OcharoCatalogue.Application.Catalogue.Publishing.Models;
using OcharoCatalogue.Application.Catalogue.Publishing.Queries;
using OcharoCatalogue.Domain.Catalogue.Entities;
using OcharoCatalogue.Domain.Catalogue.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Infrastructure.Catalogue.Publishing.QueryHandlers;

/// <summary>Handles <see cref="EditionGetAllQuery"/>.</summary>
public sealed class EditionGetAllQueryHandler(IDesignRepository designs)
    : IQueryHandler<EditionGetAllQuery, AppResult<IReadOnlyList<PublishedEditionSummary>>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<IReadOnlyList<PublishedEditionSummary>>> HandleAsync(
        EditionGetAllQuery request,
        CancellationToken ct)
    {
        var code = request.Locale ?? "uz";
        if (!CatalogueLocales.Supported(code))
        {
            var error = CatalogueErrors.Invalid("Choose a supported catalogue language.");
            return AppResult<IReadOnlyList<PublishedEditionSummary>>.Fail(error);
        }

        var records = await designs.PublicationsAsync(ct);
        var editions = records
            .Where(record => record.Locale == code)
            .Select(record => ToSummary(record, records))
            .ToArray();
        return AppResult<IReadOnlyList<PublishedEditionSummary>>.Ok(editions);
    }

    private static PublishedEditionSummary ToSummary(
        CatalogueReleaseRecord record,
        IReadOnlyList<CatalogueReleaseRecord> records)
    {
        var release = CatalogueJson.Read<CataloguePreparedRelease>(record.Json);
        var locales = records
            .Where(other => other.Slug == release.Slug)
            .Select(other => other.Locale)
            .Distinct()
            .ToArray();
        return new PublishedEditionSummary(
            release.Id,
            release.Slug,
            release.Title,
            release.Description,
            release.Locale,
            release.CreatedAtUtc,
            locales,
            release.Web is not null,
            release.PdfAsset is not null);
    }
}
