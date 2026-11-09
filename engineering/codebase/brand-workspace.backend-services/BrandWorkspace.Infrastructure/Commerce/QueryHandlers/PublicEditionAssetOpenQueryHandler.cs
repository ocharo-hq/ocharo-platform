using BrandWorkspace.Application.Commerce.Models;
using BrandWorkspace.Application.Commerce.Queries;
using BrandWorkspace.Application.Commerce.Repositories;
using BrandWorkspace.Application.Foundation.Errors;
using BrandWorkspace.Infrastructure.Commerce.Services;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Infrastructure.Commerce.QueryHandlers;

/// <summary>Handles <see cref="PublicEditionAssetOpenQuery"/>.</summary>
public sealed class PublicEditionAssetOpenQueryHandler(ICommerceRepository repository, CommerceMediaFiles files)
    : IQueryHandler<PublicEditionAssetOpenQuery, AppResult<PublicAsset>>
{
    /// <summary>Holds the refusal of an artifact the release does not serve.</summary>
    private const string Missing = "The catalogue file was not found.";

    /// <inheritdoc />
    public async ValueTask<AppResult<PublicAsset>> HandleAsync(
        PublicEditionAssetOpenQuery request,
        CancellationToken cancellationToken)
    {
        if (request.Release is null
            || await repository.PublicAsync(request.Slug, request.Release, cancellationToken) is not { } published
            || published.Snapshot.CatalogueEditions.SingleOrDefault(edition =>
                edition.Id == request.EditionId && edition.ApprovedForPublication)
                is not { } edition
            || !CommerceRules.PdfEligible(edition, published.Snapshot))
            return AppResult<PublicAsset>.Fail(DeskErrors.NotFound(Missing));
        string? hash = null;
        string? contentType = null;
        if (request.Asset == "catalogue.pdf")
        {
            hash = edition.PdfSha256;
            contentType = "application/pdf";
        }
        else
        {
            foreach (var page in edition.Pages)
            {
                if (request.Asset == $"{page.Id}.png")
                {
                    hash = page.PngSha256;
                    contentType = "image/png";
                    break;
                }
                if (request.Asset == $"{page.Id}.pdf")
                {
                    hash = page.PdfSha256;
                    contentType = "application/pdf";
                    break;
                }
            }
        }
        if (hash is null
            || await files.OpenAsync(published.BrandId, hash, request.Download, cancellationToken) is not { } blob
            || blob.ContentType != contentType)
            return AppResult<PublicAsset>.Fail(DeskErrors.NotFound(Missing));
        var fileName = request.Download ? $"{published.Snapshot.Slug}-{edition.Locale}-{request.Asset}" : null;
        return AppResult<PublicAsset>.Ok(new PublicAsset(blob, fileName));
    }
}
