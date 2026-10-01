using BrandWorkspace.Application.DesignDocuments.Models;
using BrandWorkspace.Domain.DesignDocuments.Models;

namespace BrandWorkspace.Application.DesignDocuments.Repositories;

/// <summary>Stores organization-qualified documents with serialized, immutable revision transitions.</summary>
public interface IDesignDocumentRepository
{
    Task<IReadOnlyList<DesignDocumentSummary>> GetDocumentsAsync(
        Guid organizationId,
        string? kind,
        bool includeArchived,
        int limit,
        int offset,
        CancellationToken token);

    Task<DesignDocument> GetDocumentAsync(
        Guid organizationId,
        Guid documentId,
        int? revision,
        CancellationToken token);

    Task<IReadOnlyList<DesignDocumentRevision>> GetRevisionsAsync(
        Guid organizationId,
        Guid documentId,
        int limit,
        int offset,
        CancellationToken token);

    Task<DesignDocument> CreateDocumentAsync(
        Guid organizationId,
        string actor,
        string kind,
        DesignDocumentInput input,
        CancellationToken token);

    Task<DesignDocument> SaveDocumentAsync(
        Guid organizationId,
        string actor,
        Guid documentId,
        int expectedRevision,
        DesignDocumentInput input,
        CancellationToken token);

    Task<DesignDocument> ArchiveDocumentAsync(
        Guid organizationId,
        string actor,
        Guid documentId,
        int expectedRevision,
        CancellationToken token);
}
