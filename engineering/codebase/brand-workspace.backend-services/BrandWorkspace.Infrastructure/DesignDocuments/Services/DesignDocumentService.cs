using BrandWorkspace.Application.Foundation.Errors;
using BrandWorkspace.Application.Access.Models;
using BrandWorkspace.Application.Access.Repositories;
using BrandWorkspace.Application.Commerce.Models;
using BrandWorkspace.Application.Commerce.Repositories;
using BrandWorkspace.Infrastructure.Commerce.Services;
using System.Text.Json;
using BrandWorkspace.Application.DesignDocuments.Models;
using BrandWorkspace.Application.DesignDocuments.Repositories;
using BrandWorkspace.Application.Preparation.Models;
using BrandWorkspace.Domain.DesignDocuments.Models;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace BrandWorkspace.Infrastructure.DesignDocuments.Services;

/// <summary>Enforces editing permissions and validates documents before organization-scoped persistence.</summary>
public sealed class DesignDocumentService(
    IDesignDocumentRepository repository,
    DesignDocumentValidationService validation,
    ICommerceRepository commerce)
{
    public Task<IReadOnlyList<DesignDocumentSummary>> GetDocumentsAsync(
        OrganizationAccess access,
        string? kind,
        bool includeArchived,
        int limit,
        int offset,
        CancellationToken token)
    {
        if (kind is not null) validation.ValidateKind(kind);
        validation.ValidatePage(limit, offset);
        return repository.GetDocumentsAsync(access.OrganizationId, kind, includeArchived, limit, offset, token);
    }

    public Task<Result<DesignDocument>> GetDocumentAsync(
        OrganizationAccess access,
        Guid documentId,
        CancellationToken token)
    {
        return repository.GetDocumentAsync(access.OrganizationId, documentId, null, token);
    }

    public Task<Result<DesignDocument>> GetRevisionAsync(
        OrganizationAccess access,
        Guid documentId,
        int revision,
        CancellationToken token)
    {
        validation.ValidateRevision(revision);
        return repository.GetDocumentAsync(access.OrganizationId, documentId, revision, token);
    }

    public Task<Result<IReadOnlyList<DesignDocumentRevision>>> GetRevisionsAsync(
        OrganizationAccess access,
        Guid documentId,
        int limit,
        int offset,
        CancellationToken token)
    {
        validation.ValidatePage(limit, offset);
        return repository.GetRevisionsAsync(access.OrganizationId, documentId, limit, offset, token);
    }

    public async Task<Result<DesignDocument>> CreateDocumentAsync(
        OrganizationAccess access,
        string? kind,
        DesignDocumentInput input,
        CancellationToken token)
    {
        RequireEditor(access);
        string validatedKind = validation.ValidateKind(kind);
        DesignDocumentInput validated = validation.ValidateInput(input);
        CommerceRules.Require(
            !validated.Snapshot.TryGetProperty("catalogue", out _) || validatedKind == "catalogue",
            "Catalogue metadata belongs to catalogue documents.");
        if (await ValidateCatalogueBrand(access, validated.Snapshot, token) is { } unavailable)
            return Result<DesignDocument>.Fail(unavailable);
        DesignDocument created = await repository.CreateDocumentAsync(
            access.OrganizationId,
            access.UserId,
            validatedKind,
            validated,
            token);
        return Result<DesignDocument>.Ok(created);
    }

    public async Task<Result<DesignDocument>> SaveDocumentAsync(
        OrganizationAccess access,
        Guid documentId,
        int expectedRevision,
        DesignDocumentInput input,
        CancellationToken token)
    {
        RequireEditor(access);
        validation.ValidateRevision(expectedRevision);
        DesignDocumentInput validated = validation.ValidateInput(input);
        if (validated.Snapshot.TryGetProperty("catalogue", out _))
        {
            Result<DesignDocument> current = await repository.GetDocumentAsync(
                access.OrganizationId,
                documentId,
                null,
                token);
            if (current.IsFailure(out AppError? error, out var document)) return Result<DesignDocument>.Fail(error);
            CommerceRules.Require(document.Kind == "catalogue", "Catalogue metadata belongs to catalogue documents.");
        }
        if (await ValidateCatalogueBrand(access, validated.Snapshot, token) is { } unavailable)
            return Result<DesignDocument>.Fail(unavailable);
        return await repository.SaveDocumentAsync(
            access.OrganizationId,
            access.UserId,
            documentId,
            expectedRevision,
            validated,
            token);
    }

    public Task<Result<DesignDocument>> ArchiveDocumentAsync(
        OrganizationAccess access,
        Guid documentId,
        int expectedRevision,
        CancellationToken token)
    {
        RequireEditor(access);
        validation.ValidateRevision(expectedRevision);
        return repository.ArchiveDocumentAsync(
            access.OrganizationId,
            access.UserId,
            documentId,
            expectedRevision,
            token);
    }

    /// <summary>
    /// Checks a catalogue's products and pages against its connected brand; the error when that brand is unavailable.
    /// </summary>
    private async Task<AppError?> ValidateCatalogueBrand(
        OrganizationAccess access,
        JsonElement snapshot,
        CancellationToken token)
    {
        if (CatalogueDefinition.Read(snapshot) is null) return null;
        var ids = CatalogueDefinition.ProductIdsForSnapshot(snapshot);
        if (!snapshot.TryGetProperty("catalogueSettings", out var settings)
            || !settings.TryGetProperty("brandId", out var brand))
        {
            CommerceRules.Require(ids.Count == 0, "Connect a brand before adding catalogue products.");
            return null;
        }
        var brandId = brand.GetGuid();
        Result<CommerceDraftLookup> lookup = await commerce.GetAsync(access.OrganizationId, brandId, token);
        if (lookup.IsFailure(out AppError? unavailable, out var found)) return unavailable;
        var draft = found.Draft;
        CommerceRules.Require(
            draft is not null && ids.All(id => draft.Snapshot.Products.Any(product => product.Document.Id == id)),
            "Catalogue products must belong to its connected brand.");
        if (!snapshot.TryGetProperty("pages", out var pages)) return null;
        foreach (var page in pages.EnumerateArray())
        {
            if (page.TryGetProperty("catalogueSource", out var source) && source.ValueKind == JsonValueKind.Object)
            {
                CommerceRules.Require(
                    source.TryGetProperty("brandId", out var origin)
                    && origin.TryGetGuid(out var originId)
                    && originId == brandId,
                    "Catalogue pages must belong to its connected brand.");
            }
        }
        return null;
    }

    private static void RequireEditor(OrganizationAccess access)
    {
        if (!access.CanEdit)
            throw DeskErrors.Forbidden("An editor or owner role is required.").ToException();
    }
}
