using BrandWorkspace.Application.DesignDocuments.Models;
using BrandWorkspace.Domain.DesignDocuments.Models;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

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

    /// <summary>Reads the current or a named revision; not found when the organization lacks either.</summary>
    Task<Result<DesignDocument>> GetDocumentAsync(
        Guid organizationId,
        Guid documentId,
        int? revision,
        CancellationToken token);

    /// <summary>Lists a document's revisions, newest first; not found when the organization lacks it.</summary>
    Task<Result<IReadOnlyList<DesignDocumentRevision>>> GetRevisionsAsync(
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

    /// <summary>Appends a revision at the expected one; not found, or a conflict when stale or archived.</summary>
    Task<Result<DesignDocument>> SaveDocumentAsync(
        Guid organizationId,
        string actor,
        Guid documentId,
        int expectedRevision,
        DesignDocumentInput input,
        CancellationToken token);

    /// <summary>Archives at the expected revision; not found, or a conflict when it changed since.</summary>
    Task<Result<DesignDocument>> ArchiveDocumentAsync(
        Guid organizationId,
        string actor,
        Guid documentId,
        int expectedRevision,
        CancellationToken token);
}
