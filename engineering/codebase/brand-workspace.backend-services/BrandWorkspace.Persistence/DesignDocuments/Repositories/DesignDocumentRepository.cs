using System.Data.Common;
using System.Text.Json;
using BrandWorkspace.Application.DesignDocuments.Models;
using BrandWorkspace.Application.DesignDocuments.Repositories;
using BrandWorkspace.Application.Foundation.Errors;
using BrandWorkspace.Domain.DesignDocuments.Models;
using Dapper;
using WoW.Two.Sdk.Beta.Be.Data.Abstractions;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Foundation.Serialization;

namespace BrandWorkspace.Persistence.DesignDocuments.Repositories;

/// <summary>Accesses editor snapshots and their organization-scoped, serialized revision history.</summary>
public sealed class DesignDocumentRepository(IDbConnectionFactory connectionFactory, TimeProvider time)
    : IDesignDocumentRepository
{
    /// <summary>The current revision's locale and catalogue summary, shared by the list and the single read.</summary>
    private const string SummaryColumns =
        """
        r.snapshot->>'locale' AS locale,
            CASE WHEN r.snapshot->'catalogue' IS NOT NULL THEN json_build_object(
                'slug', r.snapshot->'catalogue'->>'slug',
                'description', r.snapshot->'catalogue'->>'description',
                'isActive', r.snapshot->'catalogue'->'isActive',
                'outputs', r.snapshot->'catalogue'->'outputs',
                'web', json_build_object('theme',r.snapshot->'catalogue'->'web'->>'theme','pageCount',
                    (SELECT count(*) FROM json_array_elements(r.snapshot->'catalogue'->'web'->'pages') p
                        WHERE COALESCE(p->>'isActive','true')='true')),
                'pdfPageCount',(SELECT count(*) FROM json_array_elements(r.snapshot->'pages') p
                    WHERE COALESCE(p->>'isActive','true')='true'))::text
            END AS catalogue
        """;

    public async Task<IReadOnlyList<DesignDocumentSummary>> GetDocumentsAsync(
        Guid organizationId,
        string? kind,
        bool includeArchived,
        int limit,
        int offset,
        CancellationToken token)
    {
        const string sql =
            $"""
             SELECT d.id, d.kind, r.title, r.revision, r.archived, d.created_at, r.saved_at,
                 {SummaryColumns}
             FROM design_documents.documents d
             JOIN design_documents.revisions r
                 ON r.organization_id=d.organization_id
                 AND r.document_id=d.id
                 AND r.revision=d.current_revision
             WHERE d.organization_id=@organizationId
                 AND (@kind::text IS NULL OR d.kind=@kind)
                 AND (@includeArchived OR NOT r.archived)
             ORDER BY d.updated_at DESC, d.id
             LIMIT @limit OFFSET @offset
             """;
        await using var connection = await connectionFactory.CreateOpenAsync(token);
        IEnumerable<DocumentRow> rows = await connection.QueryAsync<DocumentRow>(
            Command(sql, new { organizationId, kind, includeArchived, limit, offset }, null, token));
        return rows.Select(row => row.ToSummary(organizationId)).ToList();
    }

    public async Task<Result<DesignDocument>> GetDocumentAsync(
        Guid organizationId,
        Guid documentId,
        int? revision,
        CancellationToken token)
    {
        await using var connection = await connectionFactory.CreateOpenAsync(token);
        return await ReadAsync(connection, null, organizationId, documentId, revision, false, token);
    }

    public async Task<Result<IReadOnlyList<DesignDocumentRevision>>> GetRevisionsAsync(
        Guid organizationId,
        Guid documentId,
        int limit,
        int offset,
        CancellationToken token)
    {
        const string documentSql =
            "SELECT id FROM design_documents.documents WHERE organization_id=@organizationId AND id=@documentId";
        const string sql =
            """
            SELECT revision, title, archived, saved_by, saved_at
            FROM design_documents.revisions
            WHERE organization_id=@organizationId
                AND document_id=@documentId
            ORDER BY revision DESC
            LIMIT @limit OFFSET @offset
            """;
        await using var connection = await connectionFactory.CreateOpenAsync(token);
        Guid? document = await connection.ExecuteScalarAsync<Guid?>(
            Command(documentSql, new { organizationId, documentId }, null, token));
        if (document is null) return Result<IReadOnlyList<DesignDocumentRevision>>.Fail(NotFound());
        IEnumerable<RevisionRow> rows = await connection.QueryAsync<RevisionRow>(
            Command(sql, new { organizationId, documentId, limit, offset }, null, token));
        List<DesignDocumentRevision> revisions = rows
            .Select(row => new DesignDocumentRevision(row.Revision, row.Title, row.Archived, row.SavedBy, row.SavedAt))
            .ToList();
        return Result<IReadOnlyList<DesignDocumentRevision>>.Ok(revisions);
    }

    public async Task<DesignDocument> CreateDocumentAsync(
        Guid organizationId,
        string actor,
        string kind,
        DesignDocumentInput input,
        CancellationToken token)
    {
        const string sql =
            """
            INSERT INTO design_documents.documents(organization_id,id,kind,current_revision,created_at,updated_at)
            VALUES(@organizationId,@id,@kind,1,@createdAt,@createdAt)
            """;
        DateTimeOffset now = time.GetUtcNow();
        var document = new DesignDocument(
            Guid.NewGuid(),
            organizationId,
            kind,
            input.Title!,
            input.Snapshot,
            1,
            false,
            now,
            now);
        await using var connection = await connectionFactory.CreateOpenAsync(token);
        await using DbTransaction transaction = await connection.BeginTransactionAsync(token);
        var parameters = new { organizationId, id = document.Id, kind, createdAt = document.CreatedAtUtc };
        await connection.ExecuteAsync(Command(sql, parameters, transaction, token));
        await InsertRevisionAsync(connection, transaction, document, actor, token);
        await transaction.CommitAsync(token);
        return document;
    }

    public async Task<Result<DesignDocument>> SaveDocumentAsync(
        Guid organizationId,
        string actor,
        Guid documentId,
        int expectedRevision,
        DesignDocumentInput input,
        CancellationToken token)
    {
        await using var connection = await connectionFactory.CreateOpenAsync(token);
        await using DbTransaction transaction = await connection.BeginTransactionAsync(token);
        Result<DesignDocument> read = await ReadAsync(
            connection,
            transaction,
            organizationId,
            documentId,
            null,
            true,
            token);
        if (read.IsFailure(out AppError? error, out var current)) return Result<DesignDocument>.Fail(error);
        if (RevisionConflict(current, expectedRevision) is { } conflict) return Result<DesignDocument>.Fail(conflict);
        if (current.Archived)
            return Result<DesignDocument>.Fail(DeskErrors.Conflict("Archived documents cannot be edited."));
        DesignDocument updated = current with
        {
            Title = input.Title!,
            Snapshot = input.Snapshot,
            Revision = checked(current.Revision + 1),
            UpdatedAtUtc = time.GetUtcNow()
        };
        await AppendAsync(connection, transaction, updated, actor, token);
        await transaction.CommitAsync(token);
        return Result<DesignDocument>.Ok(updated);
    }

    public async Task<Result<DesignDocument>> ArchiveDocumentAsync(
        Guid organizationId,
        string actor,
        Guid documentId,
        int expectedRevision,
        CancellationToken token)
    {
        await using var connection = await connectionFactory.CreateOpenAsync(token);
        await using DbTransaction transaction = await connection.BeginTransactionAsync(token);
        Result<DesignDocument> read = await ReadAsync(
            connection,
            transaction,
            organizationId,
            documentId,
            null,
            true,
            token);
        if (read.IsFailure(out AppError? error, out var current)) return Result<DesignDocument>.Fail(error);
        if (RevisionConflict(current, expectedRevision) is { } conflict) return Result<DesignDocument>.Fail(conflict);
        if (current.Archived) return Result<DesignDocument>.Ok(current);
        DesignDocument archived = current with
        {
            Archived = true,
            Revision = checked(current.Revision + 1),
            UpdatedAtUtc = time.GetUtcNow()
        };
        await AppendAsync(connection, transaction, archived, actor, token);
        await transaction.CommitAsync(token);
        return Result<DesignDocument>.Ok(archived);
    }

    private static async Task<Result<DesignDocument>> ReadAsync(
        DbConnection connection,
        DbTransaction? transaction,
        Guid organizationId,
        Guid documentId,
        int? revision,
        bool lockHead,
        CancellationToken token)
    {
        const string headSql =
            """
            SELECT id FROM design_documents.documents
            WHERE organization_id=@organizationId AND id=@documentId FOR UPDATE
            """;
        const string sql =
            $"""
             SELECT d.kind, r.title, r.snapshot::text AS snapshot, r.revision, r.archived, d.created_at, r.saved_at,
                 {SummaryColumns}
             FROM design_documents.documents d
             JOIN design_documents.revisions r
                 ON r.organization_id=d.organization_id
                 AND r.document_id=d.id
                 AND r.revision=COALESCE(@revision::integer,d.current_revision)
             WHERE d.organization_id=@organizationId
                 AND d.id=@documentId
             """;
        if (lockHead)
        {
            // Read the snapshot separately so a waiter sees the committed winner's revision after taking the lock.
            Guid? head = await connection.ExecuteScalarAsync<Guid?>(
                Command(headSql, new { organizationId, documentId }, transaction, token));
            if (head is null) return Result<DesignDocument>.Fail(NotFound());
        }
        DocumentRow? row = await connection.QueryFirstOrDefaultAsync<DocumentRow>(
            Command(sql, new { organizationId, documentId, revision }, transaction, token));
        if (row is null) return Result<DesignDocument>.Fail(NotFound());
        var document = new DesignDocument(
            documentId,
            organizationId,
            row.Kind,
            row.Title,
            JsonSerializer.Deserialize<JsonElement>(row.Snapshot!, StoredJsonConstants.Default),
            row.Revision,
            row.Archived,
            row.CreatedAt,
            row.SavedAt);
        return Result<DesignDocument>.Ok(document);
    }

    private static async Task AppendAsync(
        DbConnection connection,
        DbTransaction transaction,
        DesignDocument document,
        string actor,
        CancellationToken token)
    {
        const string sql =
            """
            UPDATE design_documents.documents
            SET current_revision=@revision, updated_at=@updatedAt
            WHERE organization_id=@organizationId
                AND id=@documentId
            """;
        await InsertRevisionAsync(connection, transaction, document, actor, token);
        var parameters = new
        {
            organizationId = document.OrganizationId,
            documentId = document.Id,
            revision = document.Revision,
            updatedAt = document.UpdatedAtUtc,
        };
        await connection.ExecuteAsync(Command(sql, parameters, transaction, token));
    }

    private static async Task InsertRevisionAsync(
        DbConnection connection,
        DbTransaction transaction,
        DesignDocument document,
        string actor,
        CancellationToken token)
    {
        const string sql =
            """
            INSERT INTO design_documents.revisions(
                organization_id,document_id,revision,title,snapshot,archived,saved_by,saved_at)
            VALUES(@organizationId,@documentId,@revision,@title,@snapshot::json,@archived,@actor,@savedAt)
            """;
        var parameters = new
        {
            organizationId = document.OrganizationId,
            documentId = document.Id,
            revision = document.Revision,
            title = document.Title,
            snapshot = JsonSerializer.Serialize(document.Snapshot, StoredJsonConstants.Default),
            archived = document.Archived,
            actor,
            savedAt = document.UpdatedAtUtc,
        };
        await connection.ExecuteAsync(Command(sql, parameters, transaction, token));
    }

    /// <summary>Refuses a save made against a revision other than the current one.</summary>
    private static AppError? RevisionConflict(DesignDocument document, int expected)
    {
        if (document.Revision == expected) return null;
        return DeskErrors.Conflict(
            $"The document changed. Current revision is {document.Revision}; preserve your work and reload.");
    }

    private static AppError NotFound()
    {
        return DeskErrors.NotFound("The document or revision was not found.");
    }

    private static CommandDefinition Command(
        string sql,
        object parameters,
        DbTransaction? transaction,
        CancellationToken token)
    {
        return new CommandDefinition(sql, parameters, transaction, cancellationToken: token);
    }

    /// <summary>One document at a revision, as its columns read; the list leaves the snapshot out.</summary>
    private sealed class DocumentRow
    {
        public Guid Id { get; init; }
        public string Kind { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string? Snapshot { get; init; }
        public int Revision { get; init; }
        public bool Archived { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
        public DateTimeOffset SavedAt { get; init; }
        public string? Locale { get; init; }
        public string? Catalogue { get; init; }

        public DesignDocumentSummary ToSummary(Guid organizationId)
        {
            return new DesignDocumentSummary(Id, organizationId, Kind, Title, Revision, Archived, CreatedAt, SavedAt)
            {
                Locale = Locale,
                Catalogue = Catalogue is null
                    ? null
                    : JsonSerializer.Deserialize<CatalogueSummary>(Catalogue, StoredJsonConstants.Default)
            };
        }
    }

    /// <summary>One saved revision's history entry.</summary>
    private sealed class RevisionRow
    {
        public int Revision { get; init; }
        public string Title { get; init; } = string.Empty;
        public bool Archived { get; init; }
        public string SavedBy { get; init; } = string.Empty;
        public DateTimeOffset SavedAt { get; init; }
    }
}
