using BrandWorkspace.Application.Commerce.Commands;
using BrandWorkspace.Application.Commerce.Repositories;
using BrandWorkspace.Application.DesignDocuments.Repositories;
using BrandWorkspace.Application.Foundation.Errors;
using BrandWorkspace.Domain.Commerce;
using BrandWorkspace.Infrastructure.Commerce.Services;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Infrastructure.Commerce.CommandHandlers;

/// <summary>Handles <see cref="CatalogueEditionAttachCommand"/>.</summary>
public sealed class CatalogueEditionAttachCommandHandler(ICommerceRepository repository, IDesignDocumentRepository designs, CommerceMediaFiles media, TimeProvider time)
    : ICommandHandler<CatalogueEditionAttachCommand, AppResult<CommerceDraft>>
{
    /// <summary>Names each edition artifact as the public download saves it: <c>{slug}-{locale}-{asset}</c>.</summary>
    private static Dictionary<string, string> EditionNames(string slug, CatalogueEdition edition)
    {
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        if (edition.PdfSha256 is { } pdf)
            names.TryAdd(pdf, $"{slug}-{edition.Locale}-catalogue.pdf");
        foreach (var page in edition.Pages)
        {
            names.TryAdd(page.PngSha256, $"{slug}-{edition.Locale}-{page.Id}.png");
            names.TryAdd(page.PdfSha256, $"{slug}-{edition.Locale}-{page.Id}.pdf");
        }
        return names;
    }

    /// <inheritdoc />
    public async ValueTask<AppResult<CommerceDraft>> HandleAsync(CatalogueEditionAttachCommand request, CancellationToken cancellationToken)
    {
        if (!request.Access.CanEdit)
            return AppResult<CommerceDraft>.Fail(DeskErrors.Forbidden("Editor access is required."));
        var access = request.Access;
        if (await repository.GetAsync(access.OrganizationId, request.BrandId, cancellationToken) is not { } draft)
            return AppResult<CommerceDraft>.Fail(DeskErrors.NotFound("Save the brand draft first."));
        if (draft.Revision != request.ExpectedRevision)
            return AppResult<CommerceDraft>.Fail(DeskErrors.Conflict("The draft changed. Reload before attaching the edition."));
        var design = await designs.GetDocumentAsync(access.OrganizationId, request.SourceDesignId, request.SourceRevision, cancellationToken);
        var built = CatalogueEditionBuilder.Build(design, request.BrandId, request.Files, time.GetUtcNow(), draft.Snapshot);
        // The artifacts land in Files before the attach's transaction, named as the public download saves them.
        var blobs = await media.StoreAsync(request.BrandId, built.Blobs, CatalogueEditionBuilder.MaximumPdfBytes,
            EditionNames(draft.Snapshot.Slug, built.Edition), cancellationToken);
        var saved = await repository.AttachCatalogueEditionAsync(access.OrganizationId, access.UserId, request.BrandId,
            request.ExpectedRevision, built.Edition, blobs, cancellationToken);
        return AppResult<CommerceDraft>.Ok(saved);
    }
}
