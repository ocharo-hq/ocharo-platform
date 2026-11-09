using OcharoCatalogue.Application.Catalogue.Core.Errors;
using OcharoCatalogue.Application.Catalogue.Core.Repositories;
using OcharoCatalogue.Application.Catalogue.Core.Serializers;
using OcharoCatalogue.Application.Catalogue.Publishing.Models;
using OcharoCatalogue.Application.Catalogue.Publishing.Queries;
using OcharoCatalogue.Domain.Catalogue.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Infrastructure.Catalogue.Publishing.QueryHandlers;

/// <summary>Handles <see cref="EditionGetBySlugQuery"/>.</summary>
public sealed class EditionGetBySlugQueryHandler(IDesignRepository designs)
    : IQueryHandler<EditionGetBySlugQuery, AppResult<PublishedEdition>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<PublishedEdition>> HandleAsync(EditionGetBySlugQuery request, CancellationToken ct)
    {
        var code = request.Locale ?? "uz";
        if (!CatalogueLocales.Supported(code))
        {
            return AppResult<PublishedEdition>.Fail(CatalogueErrors.Invalid("Choose a supported catalogue language."));
        }

        var record = await designs.PublishedAsync(
            request.Slug,
            code,
            request.ReleaseId,
            ct);
        if (record is null)
        {
            return AppResult<PublishedEdition>.Fail(CatalogueErrors.NotFound("Published catalogue"));
        }

        var release = CatalogueJson.Read<CataloguePreparedRelease>(record.Json);
        var available = (await designs.PublicationsAsync(ct))
            .Where(publication => publication.Slug == request.Slug)
            .Select(publication => publication.Locale)
            .Distinct()
            .ToArray();
        var edition = new PublishedEdition(
            release.Id,
            release.DesignId,
            release.DesignRevision,
            release.Slug,
            release.Title,
            release.Description,
            release.Locale,
            release.CreatedAtUtc,
            record.PublishedAtUtc,
            available,
            release.Web,
            release.Products,
            Pdf(
                release,
                request.Slug,
                code));
        return AppResult<PublishedEdition>.Ok(edition);
    }

    private static PublishedEditionPdf? Pdf(
        CataloguePreparedRelease release,
        string slug,
        string code)
    {
        if (release.PdfAsset is null)
        {
            return null;
        }

        string Url(string asset)
        {
            return $"/api/public/catalogue/editions/{Uri.EscapeDataString(slug)}/assets/{Uri.EscapeDataString(asset)}"
                + $"?locale={Uri.EscapeDataString(code)}&release={release.Id:D}";
        }

        var pages = release.Pages
            .Select((page, index) => new PublishedEditionPage(
                page.Id,
                index + 1,
                page.Title,
                page.WidthMm,
                page.HeightMm,
                page.ProductIds,
                Url(page.PngAsset),
                Url(page.PdfAsset)))
            .ToArray();
        return new PublishedEditionPdf(Url(release.PdfAsset), pages);
    }
}
