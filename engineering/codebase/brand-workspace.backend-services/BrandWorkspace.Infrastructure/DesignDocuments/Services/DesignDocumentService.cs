using BrandWorkspace.Application.Access;
using BrandWorkspace.Application.DesignDocuments.Models;
using BrandWorkspace.Application.DesignDocuments.Repositories;
using BrandWorkspace.Application.Preparation.Models;
using BrandWorkspace.Domain.DesignDocuments.Models;

namespace BrandWorkspace.Infrastructure.DesignDocuments.Services;

/// <summary>Enforces editing permissions and validates documents before organization-scoped persistence.</summary>
public sealed class DesignDocumentService(
    IDesignDocumentRepository repository,
    DesignDocumentValidationService validation)
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

    public Task<DesignDocument> GetDocumentAsync(
        OrganizationAccess access,
        Guid documentId,
        CancellationToken token)
    {
        return repository.GetDocumentAsync(access.OrganizationId, documentId, null, token);
    }

    public Task<DesignDocument> GetRevisionAsync(
        OrganizationAccess access,
        Guid documentId,
        int revision,
        CancellationToken token)
    {
        validation.ValidateRevision(revision);
        return repository.GetDocumentAsync(access.OrganizationId, documentId, revision, token);
    }

    public Task<IReadOnlyList<DesignDocumentRevision>> GetRevisionsAsync(
        OrganizationAccess access,
        Guid documentId,
        int limit,
        int offset,
        CancellationToken token)
    {
        validation.ValidatePage(limit, offset);
        return repository.GetRevisionsAsync(access.OrganizationId, documentId, limit, offset, token);
    }

    public Task<DesignDocument> CreateDocumentAsync(
        OrganizationAccess access,
        string? kind,
        DesignDocumentInput input,
        CancellationToken token)
    {
        RequireEditor(access);
        string validatedKind = validation.ValidateKind(kind);
        DesignDocumentInput validated = validation.ValidateInput(input);
        return repository.CreateDocumentAsync(access.OrganizationId, access.UserId, validatedKind, validated, token);
    }

    public Task<DesignDocument> SaveDocumentAsync(
        OrganizationAccess access,
        Guid documentId,
        int expectedRevision,
        DesignDocumentInput input,
        CancellationToken token)
    {
        RequireEditor(access);
        validation.ValidateRevision(expectedRevision);
        DesignDocumentInput validated = validation.ValidateInput(input);
        return repository.SaveDocumentAsync(
            access.OrganizationId,
            access.UserId,
            documentId,
            expectedRevision,
            validated,
            token);
    }

    public Task<DesignDocument> ArchiveDocumentAsync(
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

    private static void RequireEditor(OrganizationAccess access)
    {
        if (!access.CanEdit)
            throw new PreparationException("forbidden", "An editor or owner role is required.");
    }
}
