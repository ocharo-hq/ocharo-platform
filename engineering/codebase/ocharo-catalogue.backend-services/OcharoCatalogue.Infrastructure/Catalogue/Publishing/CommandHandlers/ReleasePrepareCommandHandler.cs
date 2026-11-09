using System.Text.Json;
using OcharoCatalogue.Application.Catalogue.Core.Errors;
using OcharoCatalogue.Application.Catalogue.Core.Repositories;
using OcharoCatalogue.Application.Catalogue.Core.Serializers;
using OcharoCatalogue.Application.Catalogue.Designs.Models;
using OcharoCatalogue.Application.Catalogue.Publishing.Commands;
using OcharoCatalogue.Application.Catalogue.Publishing.Constants;
using OcharoCatalogue.Application.Catalogue.Publishing.Models;
using OcharoCatalogue.Application.Catalogue.Publishing.Services;
using OcharoCatalogue.Domain.Catalogue.Entities;
using OcharoCatalogue.Infrastructure.Catalogue.Designs.Validators;
using OcharoCatalogue.Infrastructure.Catalogue.Publishing.FoundationServices;
using OcharoCatalogue.Infrastructure.Catalogue.Publishing.Mappers;
using OcharoCatalogue.Infrastructure.Catalogue.Publishing.Models;
using OcharoCatalogue.Infrastructure.Catalogue.Publishing.Policies;
using OcharoCatalogue.Infrastructure.Catalogue.Publishing.Trackers;
using OcharoCatalogue.Infrastructure.Catalogue.Settings.FoundationServices;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Infrastructure.Catalogue.Publishing.CommandHandlers;

/// <summary>Handles <see cref="ReleasePrepareCommand"/>.</summary>
public sealed class ReleasePrepareCommandHandler(
    ReleasePreparationService preparations,
    IDesignRepository designs,
    CatalogueSettingsService settings,
    IDesignArtifactValidator artifacts,
    IReleaseAssetStore releaseAssets,
    TimeProvider timeProvider) : ICommandHandler<ReleasePrepareCommand, AppResult<CataloguePreparedRelease>>
{
    private const int MaximumPageBytes = 15 * 1024 * 1024;
    private const string InvalidMetadata = "Catalogue preparation metadata is invalid.";

    /// <inheritdoc />
    public async ValueTask<AppResult<CataloguePreparedRelease>> HandleAsync(
        ReleasePrepareCommand request,
        CancellationToken ct)
    {
        var metadata = request.Metadata;
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        if (request.FormMetadata is { } form)
        {
            if (form.Count != 1
                || form[0].Length > ReleaseLimits.MaximumMetadataLength
                || request.Files.Count > ReleaseLimits.MaximumFiles)
            {
                return AppResult<CataloguePreparedRelease>.Fail(CatalogueErrors.Invalid(InvalidMetadata));
            }

            if (!TryRead(form[0], out metadata))
            {
                return AppResult<CataloguePreparedRelease>.Fail(CatalogueErrors.Invalid(InvalidMetadata));
            }
        }

        if (Collect(request.Files, files) is { } refused)
        {
            return AppResult<CataloguePreparedRelease>.Fail(refused);
        }

        if (metadata is null
            || metadata.ExpectedRevision < 1
            || metadata.Fingerprint is not { Length: 64 }
            || !metadata.Fingerprint.All(char.IsAsciiHexDigit))
        {
            var error = CatalogueErrors.Invalid("Get preparation details before rendering this saved catalogue.");
            return AppResult<CataloguePreparedRelease>.Fail(error);
        }

        var read = await preparations.ReadAsync(
            request.DesignId,
            metadata.ExpectedRevision,
            ct);
        if (read is AppResult<PreparationContext>.Failure failure)
        {
            return AppResult<CataloguePreparedRelease>.Fail(failure.Error);
        }

        var preparation = ((AppResult<PreparationContext>.Success)read).Data;
        if (preparation.Fingerprint != metadata.Fingerprint)
        {
            var error = CatalogueErrors.Conflict(
                "Product information changed while rendering. Review and render again.");
            return AppResult<CataloguePreparedRelease>.Fail(error);
        }

        return await PrepareAsync(
            preparation,
            metadata.ExpectedRevision,
            files,
            ct);
    }

    private async Task<AppResult<CataloguePreparedRelease>> PrepareAsync(
        PreparationContext preparation,
        int expected,
        IReadOnlyDictionary<string, byte[]> files,
        CancellationToken ct)
    {
        CatalogueDesignValidator.Require(
            files.Values.Sum(value => (long)value.Length) <= ReleaseLimits.MaximumTotalBytes,
            "Catalogue artifacts exceed 256 MiB.");
        var uploads = new ReleaseArtifactTracker(files, artifacts);
        var pages = new List<CatalogueReleasePage>();
        foreach (var page in ReleaseSelectionPolicy.PdfPages(preparation))
        {
            var pageId = page.GetProperty("id").GetString()!;
            var width = page.GetProperty("width").GetDouble();
            var height = page.GetProperty("height").GetDouble();
            uploads.AddPng(
                $"page-{pageId}.png",
                MaximumPageBytes,
                width / height);
            uploads.AddPdf(
                $"page-{pageId}.pdf",
                MaximumPageBytes,
                [new CataloguePdfPageSize(width, height)]);
            pages.Add(new CatalogueReleasePage(
                pageId,
                page.GetProperty("title").GetString()!,
                width,
                height,
                ReleaseSelectionPolicy.References(page, preparation.Products),
                $"page-{pageId}.png",
                $"page-{pageId}.pdf"));
        }

        string? pdf = null;
        if (preparation.Definition.Outputs.Pdf)
        {
            if (pages.Count == 0)
            {
                var error = CatalogueErrors.Invalid("No active PDF pages contain confirmed products in this language.");
                return AppResult<CataloguePreparedRelease>.Fail(error);
            }

            pdf = "catalogue.pdf";
            uploads.AddPdf(
                pdf,
                ReleaseLimits.MaximumFileBytes,
                pages.Select(page => new CataloguePdfPageSize(page.WidthMm, page.HeightMm)).ToArray());
        }

        if (!uploads.Expected.SetEquals(files.Keys))
        {
            var error = CatalogueErrors.Invalid(
                "Upload exactly the selected PDF output files; no additional artifacts are accepted.");
            return AppResult<CataloguePreparedRelease>.Fail(error);
        }

        var webPages = WebPages(preparation);
        if (pages.Count == 0 && webPages.Length == 0)
        {
            var error = CatalogueErrors.Invalid(
                "The catalogue has no active pages with confirmed products for this language.");
            return AppResult<CataloguePreparedRelease>.Fail(error);
        }

        var publicProducts = await PublicProductsAsync(
            preparation,
            pages,
            webPages,
            ct);
        JsonElement? web = webPages.Length > 0
            ? ReleaseWebMapper.ToWeb(
                preparation,
                webPages,
                publicProducts)
            : null;
        var release = new CataloguePreparedRelease(
            Guid.NewGuid(),
            preparation.Design.Id,
            preparation.Design.Revision,
            preparation.Definition.Slug,
            preparation.Design.Title,
            preparation.Definition.Description,
            preparation.Design.Locale,
            timeProvider.GetUtcNow(),
            web,
            publicProducts,
            pages,
            pdf,
            uploads.Links);

        // The artifacts reach their store before the release's transaction, which records where they went; a refused
        // transaction leaves them stored, and preparing again finds them by SHA-256.
        var artifactsToStore = uploads.Stored
            .Select(upload => new ReleaseArtifact(
                upload.Sha256,
                upload.Type,
                ReleaseDownloadNames.For(release, upload.Name),
                upload.Bytes))
            .DistinctBy(artifact => artifact.Sha256)
            .ToArray();
        var storage = await releaseAssets.StoreAsync(artifactsToStore, ct);
        var record = new CatalogueReleaseRecord
        {
            Id = release.Id,
            DesignId = preparation.Design.Id,
            DesignRevision = expected,
            Slug = release.Slug,
            Locale = release.Locale,
            CreatedAtUtc = release.CreatedAtUtc,
            Fingerprint = preparation.Fingerprint,
            ProductIdsJson = CatalogueJson.Write(preparation.References),
            Json = CatalogueJson.Write(release),
        };
        await designs.PrepareAsync(
            record,
            storage.Rows,
            storage.Files,
            ct);
        return AppResult<CataloguePreparedRelease>.Ok(release);
    }

    private async Task<PublicProduct[]> PublicProductsAsync(
        PreparationContext preparation,
        IReadOnlyList<CatalogueReleasePage> pages,
        IReadOnlyList<CatalogueWebPage> webPages,
        CancellationToken ct)
    {
        var productIds = pages
            .SelectMany(page => page.ProductIds)
            .Concat(webPages.SelectMany(page => page.ProductIds))
            .Distinct()
            .ToArray();
        var current = await settings.GetAsync(ct);
        var garments = current.GarmentTypes.ToDictionary(garment => garment.Key);
        return productIds
            .Select(productId =>
            {
                var row = preparation.Rows.Single(candidate => candidate.Id == productId);
                return PublicProductMapper.ToPublic(
                    preparation.Products[productId],
                    row.Revision,
                    row.UpdatedAt,
                    preparation.Design.Locale,
                    garments);
            })
            .ToArray();
    }

    private static CatalogueWebPage[] WebPages(PreparationContext preparation)
    {
        if (!preparation.Definition.Outputs.Web)
        {
            return [];
        }

        return preparation.Definition.WebPages
            .Where(page => page.IsActive)
            .Select(page => page with
            {
                ProductIds = page.ProductIds.Where(id => ReleaseSelectionPolicy.Eligible(preparation, id)).ToArray(),
            })
            .Where(page => page.ProductIds.Count > 0)
            .ToArray();
    }

    private static bool TryRead(string json, out ReleasePrepareMetadata? metadata)
    {
        try
        {
            metadata = JsonSerializer.Deserialize<ReleasePrepareMetadata>(json, CatalogueJson.Options);
            return true;
        }
        catch (JsonException)
        {
            metadata = null;
            return false;
        }
    }

    private static AppError? Collect(IReadOnlyList<ReleaseUpload> uploads, Dictionary<string, byte[]> files)
    {
        long total = 0;
        foreach (var upload in uploads)
        {
            total += upload.Bytes.Length;
            if (upload.Bytes.Length <= 0
                || upload.Bytes.Length > ReleaseLimits.MaximumFileBytes
                || total > ReleaseLimits.MaximumTotalBytes
                || files.ContainsKey(upload.FileName))
            {
                return CatalogueErrors.Invalid("Catalogue artifacts are oversized or duplicated.");
            }

            files.Add(upload.FileName, upload.Bytes);
        }

        return null;
    }
}
