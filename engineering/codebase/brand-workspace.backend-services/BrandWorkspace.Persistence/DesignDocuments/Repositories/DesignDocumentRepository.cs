using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using WoW.Two.Sdk.Backend.Beta.Foundation.Serialization;
using BrandWorkspace.Application.DesignDocuments.Models;
using BrandWorkspace.Application.DesignDocuments.Repositories;
using BrandWorkspace.Application.Preparation.Models;
using BrandWorkspace.Domain.DesignDocuments.Models;

namespace BrandWorkspace.Persistence.DesignDocuments.Repositories;

/// <summary>Persists editor snapshots and serializes organization-scoped revision changes in PostgreSQL.</summary>
public sealed class DesignDocumentRepository(NpgsqlDataSource dataSource, TimeProvider time)
    : IDesignDocumentRepository
{
    public async Task<IReadOnlyList<DesignDocumentSummary>> GetDocumentsAsync(
        Guid organizationId,
        string? kind,
        bool includeArchived,
        int limit,
        int offset,
        CancellationToken token)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(token);
        await using NpgsqlCommand command = Command(
            connection,
            null,
            """
            SELECT d.id, d.kind, r.title, r.revision, r.archived, d.created_at, r.saved_at
            FROM design_documents.documents d
            JOIN design_documents.revisions r
                ON r.organization_id=d.organization_id
                AND r.document_id=d.id
                AND r.revision=d.current_revision
            WHERE d.organization_id=$1
                AND ($2::text IS NULL OR d.kind=$2)
                AND ($3 OR NOT r.archived)
            ORDER BY d.updated_at DESC, d.id
            LIMIT $4 OFFSET $5
            """,
            organizationId,
            kind,
            includeArchived,
            limit,
            offset);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(token);
        var documents = new List<DesignDocumentSummary>();
        while (await reader.ReadAsync(token))
        {
            documents.Add(new DesignDocumentSummary(
                reader.GetGuid(0),
                organizationId,
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt32(3),
                reader.GetBoolean(4),
                reader.GetFieldValue<DateTimeOffset>(5),
                reader.GetFieldValue<DateTimeOffset>(6)));
        }
        return documents;
    }

    public async Task<DesignDocument> GetDocumentAsync(
        Guid organizationId,
        Guid documentId,
        int? revision,
        CancellationToken token)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(token);
        return await ReadAsync(connection, null, organizationId, documentId, revision, false, token);
    }

    public async Task<IReadOnlyList<DesignDocumentRevision>> GetRevisionsAsync(
        Guid organizationId,
        Guid documentId,
        int limit,
        int offset,
        CancellationToken token)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(token);
        await using (NpgsqlCommand document = Command(
            connection,
            null,
            "SELECT id FROM design_documents.documents WHERE organization_id=$1 AND id=$2",
            organizationId,
            documentId))
        {
            if (await document.ExecuteScalarAsync(token) is null) throw NotFound();
        }
        await using NpgsqlCommand command = Command(
            connection,
            null,
            """
            SELECT revision, title, archived, saved_by, saved_at
            FROM design_documents.revisions
            WHERE organization_id=$1
                AND document_id=$2
            ORDER BY revision DESC
            LIMIT $3 OFFSET $4
            """,
            organizationId,
            documentId,
            limit,
            offset);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(token);
        var revisions = new List<DesignDocumentRevision>();
        while (await reader.ReadAsync(token))
        {
            revisions.Add(new DesignDocumentRevision(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetBoolean(2),
                reader.GetString(3),
                reader.GetFieldValue<DateTimeOffset>(4)));
        }
        return revisions;
    }

    public async Task<DesignDocument> CreateDocumentAsync(
        Guid organizationId,
        string actor,
        string kind,
        DesignDocumentInput input,
        CancellationToken token)
    {
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
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(token);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(token);
        await using (NpgsqlCommand insert = Command(
            connection,
            transaction,
            """
            INSERT INTO design_documents.documents(organization_id,id,kind,current_revision,created_at,updated_at)
            VALUES($1,$2,$3,1,$4,$4)
            """,
            organizationId,
            document.Id,
            kind,
            document.CreatedAtUtc))
        {
            await insert.ExecuteNonQueryAsync(token);
        }
        await InsertRevisionAsync(connection, transaction, document, actor, token);
        await transaction.CommitAsync(token);
        return document;
    }

    public async Task<DesignDocument> SaveDocumentAsync(
        Guid organizationId,
        string actor,
        Guid documentId,
        int expectedRevision,
        DesignDocumentInput input,
        CancellationToken token)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(token);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(token);
        DesignDocument current = await ReadAsync(
            connection, transaction, organizationId, documentId, null, true, token);
        RequireRevision(current, expectedRevision);
        if (current.Archived)
            throw new PreparationException("conflict", "Archived documents cannot be edited.");
        DesignDocument updated = current with
        {
            Title = input.Title!,
            Snapshot = input.Snapshot,
            Revision = checked(current.Revision + 1),
            UpdatedAtUtc = time.GetUtcNow()
        };
        await AppendAsync(connection, transaction, updated, actor, token);
        await transaction.CommitAsync(token);
        return updated;
    }

    public async Task<DesignDocument> ArchiveDocumentAsync(
        Guid organizationId,
        string actor,
        Guid documentId,
        int expectedRevision,
        CancellationToken token)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(token);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(token);
        DesignDocument current = await ReadAsync(
            connection, transaction, organizationId, documentId, null, true, token);
        RequireRevision(current, expectedRevision);
        if (current.Archived) return current;
        DesignDocument archived = current with
        {
            Archived = true,
            Revision = checked(current.Revision + 1),
            UpdatedAtUtc = time.GetUtcNow()
        };
        await AppendAsync(connection, transaction, archived, actor, token);
        await transaction.CommitAsync(token);
        return archived;
    }

    private static async Task<DesignDocument> ReadAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid organizationId,
        Guid documentId,
        int? revision,
        bool lockHead,
        CancellationToken token)
    {
        if (lockHead)
        {
            // Read the snapshot separately so a waiter sees the committed winner's revision after taking the lock.
            await using NpgsqlCommand head = Command(
                connection,
                transaction,
                "SELECT id FROM design_documents.documents WHERE organization_id=$1 AND id=$2 FOR UPDATE",
                organizationId,
                documentId);
            if (await head.ExecuteScalarAsync(token) is null) throw NotFound();
        }
        await using NpgsqlCommand command = Command(
            connection,
            transaction,
            """
            SELECT d.kind, r.title, r.snapshot::text, r.revision, r.archived, d.created_at, r.saved_at
            FROM design_documents.documents d
            JOIN design_documents.revisions r
                ON r.organization_id=d.organization_id
                AND r.document_id=d.id
                AND r.revision=COALESCE($3::integer,d.current_revision)
            WHERE d.organization_id=$1
                AND d.id=$2
            """,
            organizationId,
            documentId,
            revision);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(token);
        if (!await reader.ReadAsync(token)) throw NotFound();
        return new DesignDocument(
            documentId,
            organizationId,
            reader.GetString(0),
            reader.GetString(1),
            JsonSerializer.Deserialize<JsonElement>(reader.GetString(2), StoredJsonConstants.Default),
            reader.GetInt32(3),
            reader.GetBoolean(4),
            reader.GetFieldValue<DateTimeOffset>(5),
            reader.GetFieldValue<DateTimeOffset>(6));
    }

    private static async Task AppendAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        DesignDocument document,
        string actor,
        CancellationToken token)
    {
        await InsertRevisionAsync(connection, transaction, document, actor, token);
        await using NpgsqlCommand update = Command(
            connection,
            transaction,
            """
            UPDATE design_documents.documents
            SET current_revision=$3, updated_at=$4
            WHERE organization_id=$1
                AND id=$2
            """,
            document.OrganizationId,
            document.Id,
            document.Revision,
            document.UpdatedAtUtc);
        await update.ExecuteNonQueryAsync(token);
    }

    private static async Task InsertRevisionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        DesignDocument document,
        string actor,
        CancellationToken token)
    {
        await using NpgsqlCommand insert = Command(
            connection,
            transaction,
            """
            INSERT INTO design_documents.revisions(
                organization_id,document_id,revision,title,snapshot,archived,saved_by,saved_at)
            VALUES($1,$2,$3,$4,$5::json,$6,$7,$8)
            """,
            document.OrganizationId,
            document.Id,
            document.Revision,
            document.Title,
            JsonSerializer.Serialize(document.Snapshot, StoredJsonConstants.Default),
            document.Archived,
            actor,
            document.UpdatedAtUtc);
        await insert.ExecuteNonQueryAsync(token);
    }

    private static void RequireRevision(DesignDocument document, int expected)
    {
        if (document.Revision != expected)
            throw new PreparationException(
                "conflict",
                $"The document changed. Current revision is {document.Revision}; preserve your work and reload.");
    }

    private static PreparationException NotFound()
    {
        return new PreparationException("not-found", "The document or revision was not found.");
    }

    private static NpgsqlCommand Command(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string sql,
        params object?[] values)
    {
        var command = new NpgsqlCommand(sql, connection, transaction);
        foreach (object? value in values)
        {
            if (value is null)
                command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Unknown, Value = DBNull.Value });
            else command.Parameters.AddWithValue(value);
        }
        return command;
    }
}
