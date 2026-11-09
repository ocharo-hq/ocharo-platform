using System.Text.Json;
using OcharoCatalogue.Application.Catalogue.Core.Errors;
using OcharoCatalogue.Application.Catalogue.Core.Repositories;
using OcharoCatalogue.Application.Catalogue.Core.Serializers;
using OcharoCatalogue.Application.Catalogue.Designs.Models;
using OcharoCatalogue.Application.Catalogue.Publishing.Hashers;
using OcharoCatalogue.Domain.Catalogue.Entities;
using OcharoCatalogue.Domain.Catalogue.Models;
using OcharoCatalogue.Infrastructure.Catalogue.Core.Policies;
using OcharoCatalogue.Infrastructure.Catalogue.Designs.Validators;
using OcharoCatalogue.Infrastructure.Catalogue.Publishing.Models;
using OcharoCatalogue.Infrastructure.Catalogue.Publishing.Policies;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Infrastructure.Catalogue.Publishing.FoundationServices;

/// <summary>Provides the read of a saved design for preparation: an active, current design whose PDF products match
/// their saved revisions, with every image it shows intact, and the fingerprint of what it reads.</summary>
public sealed class ReleasePreparationService(
    IDesignRepository designs,
    ICatalogueRepository repository,
    MediaVerificationService media)
{
    /// <summary>
    /// Reads design <paramref name="id"/> for preparation, at <paramref name="expected"/> when one is given.
    /// </summary>
    /// <param name="id">The design id.</param>
    /// <param name="expected">The design revision the caller rendered; null to read the current one.</param>
    /// <param name="ct">The token that cancels the read.</param>
    public async Task<AppResult<PreparationContext>> ReadAsync(
        Guid id,
        int? expected,
        CancellationToken ct)
    {
        var design = await designs.GetAsync(id, ct);
        if (design is null)
        {
            return AppResult<PreparationContext>.Fail(CatalogueErrors.NotFound("Catalogue design"));
        }

        if (design.Archived || (expected is not null && design.Revision != expected))
        {
            var error = CatalogueErrors.Conflict("The catalogue changed or was archived. Reload before preparing.");
            return AppResult<PreparationContext>.Fail(error);
        }

        var input = new CatalogueDesignInput(design.Title, CatalogueJson.Read<JsonElement>(design.Json));
        var snapshot = CatalogueDesignValidator.Validate(input);
        var definition = CatalogueDesignValidator.Definition(snapshot);
        if (!definition.IsActive)
        {
            return AppResult<PreparationContext>.Fail(
                CatalogueErrors.Invalid("Activate the catalogue before preparing a release."));
        }

        var rows = await repository.GetProductsAsync(ct);
        var products = rows.ToDictionary(
            row => row.Id,
            row => CatalogueJson.Read<ProductDocument>(row.Json),
            StringComparer.Ordinal);
        var references = CatalogueDesignValidator.References(snapshot)
            .Concat(snapshot.GetProperty("pages").EnumerateArray()
                .SelectMany(page => ReleaseSelectionPolicy.References(page, products)))
            .Distinct()
            .ToArray();
        if (definition.Outputs.Pdf && !PdfProductsCurrent(
            snapshot,
            rows,
            products))
        {
            var error = CatalogueErrors.Conflict(
                "PDF product information is stale. Refresh the products and save the catalogue before preparing.");
            return AppResult<PreparationContext>.Fail(error);
        }

        var fingerprint = DesignFingerprint.Create(
            rows,
            await repository.FindSettingsAsync(ct),
            references);
        var preparation = new PreparationContext(
            design,
            snapshot,
            definition,
            rows,
            products,
            references,
            fingerprint);
        if (await media.VerifyAsync(ShownImages(preparation), ct) is { } failure)
        {
            return AppResult<PreparationContext>.Fail(failure);
        }

        return AppResult<PreparationContext>.Ok(preparation);
    }

    private static bool PdfProductsCurrent(
        JsonElement snapshot,
        IReadOnlyList<ProductRecord> rows,
        IReadOnlyDictionary<string, ProductDocument> products)
    {
        var pages = snapshot.GetProperty("pages").EnumerateArray().Where(CatalogueDesignValidator.Active);
        foreach (var page in pages)
        {
            foreach (var productId in ReleaseSelectionPolicy.References(page, products))
            {
                var sourceRevision = SourceRevision(
                    snapshot,
                    page,
                    productId);
                if (sourceRevision == 0 || rows.All(row => row.Id != productId || row.Revision != sourceRevision))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static int SourceRevision(
        JsonElement snapshot,
        JsonElement page,
        string productId)
    {
        var sourceRevision = 0;
        if (snapshot.TryGetProperty("productRevisions", out var evidence)
            && evidence.TryGetProperty(productId, out var saved))
        {
            saved.TryGetInt32(out sourceRevision);
        }

        if (sourceRevision == 0
            && page.TryGetProperty("catalogueSource", out var origin)
            && origin.TryGetProperty("productId", out var sourceId)
            && sourceId.GetString() == productId
            && origin.TryGetProperty("productRevision", out var productRevision))
        {
            productRevision.TryGetInt32(out sourceRevision);
        }

        return sourceRevision;
    }

    private static IEnumerable<string> ShownImages(PreparationContext preparation)
    {
        var pdfHashes = ReleaseSelectionPolicy.PdfPages(preparation)
            .SelectMany(page => page.GetProperty("elements").EnumerateArray())
            .Where(element => element.TryGetProperty("src", out _))
            .Select(element => CatalogueDesignValidator.ImageHash(element.GetProperty("src").GetString()!))
            .OfType<string>();
        if (!preparation.Definition.Outputs.Web)
        {
            return pdfHashes;
        }

        var webHashes = preparation.Definition.WebPages
            .Where(page => page.IsActive)
            .SelectMany(page => page.ProductIds)
            .Distinct()
            .Where(id => ReleaseSelectionPolicy.Eligible(preparation, id))
            .SelectMany(id => ProductPublicationEligibility.ActiveImages(preparation.Products[id]))
            .Select(image => image.Sha256);
        return pdfHashes.Concat(webHashes);
    }
}
