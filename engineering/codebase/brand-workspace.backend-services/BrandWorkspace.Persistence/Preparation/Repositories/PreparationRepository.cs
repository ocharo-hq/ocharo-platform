using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BrandWorkspace.Application.Preparation.Models;
using BrandWorkspace.Application.Preparation.Repositories;
using BrandWorkspace.Application.Preparation.Services;
using BrandWorkspace.Domain.Preparation.Models;
using Npgsql;
using NpgsqlTypes;
using WoW.Two.Sdk.Beta.Be.Foundation.Serialization;

namespace BrandWorkspace.Persistence.Preparation.Repositories;

/// <summary>PostgreSQL storage with organization-qualified access and serialized draft transitions.</summary>
public sealed partial class PreparationRepository(NpgsqlDataSource dataSource, TimeProvider time, IManualPackageBuilder packages)
    : IPreparationRepository
{
    public async Task<IReadOnlyList<Brand>> GetBrandsAsync(Guid organizationId, CancellationToken token)
    {
        await using var connection = await dataSource.OpenConnectionAsync(token);
        await using var command = Command(connection, null,
            "SELECT id,name,time_zone FROM preparation.brands WHERE organization_id=$1 ORDER BY name,id", organizationId);
        await using var reader = await command.ExecuteReaderAsync(token);
        var brands = new List<Brand>();
        while (await reader.ReadAsync(token)) brands.Add(new Brand(reader.GetGuid(0), organizationId, reader.GetString(1), reader.GetString(2)));
        return brands;
    }

    public async Task<Brand> CreateBrandAsync(Guid organizationId, string name, string timeZone, CancellationToken token)
    {
        var brand = new Brand(Guid.NewGuid(), organizationId, name, timeZone);
        await using var connection = await dataSource.OpenConnectionAsync(token);
        await using var command = Command(connection, null,
            "INSERT INTO preparation.brands(organization_id,id,name,time_zone) VALUES($1,$2,$3,$4)",
            organizationId, brand.Id, name, timeZone);
        await command.ExecuteNonQueryAsync(token);
        return brand;
    }

    public async Task<IReadOnlyList<MediaAsset>> GetMediaAsync(Guid organizationId, CancellationToken token)
    {
        await using var connection = await dataSource.OpenConnectionAsync(token);
        await using var command = Command(connection, null,
            "SELECT id,file_name,content_type,octet_length(bytes)::bigint,sha256,provenance,created_at FROM preparation.media WHERE organization_id=$1 ORDER BY created_at DESC,id",
            organizationId);
        await using var reader = await command.ExecuteReaderAsync(token);
        var media = new List<MediaAsset>();
        while (await reader.ReadAsync(token)) media.Add(ReadMedia(reader, organizationId));
        return media;
    }

    public async Task<MediaAsset> CreateMediaAsync(Guid organizationId, string actor, string fileName, string provenance,
        ValidatedImage image, CancellationToken token)
    {
        var media = new MediaAsset(Guid.NewGuid(), organizationId, fileName, image.ContentType, image.Bytes.LongLength,
            image.Sha256, provenance, time.GetUtcNow());
        await using var connection = await dataSource.OpenConnectionAsync(token);
        await using var command = Command(connection, null,
            """
            INSERT INTO preparation.media(organization_id,id,file_name,content_type,bytes,sha256,provenance,created_by,created_at)
            VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9)
            """, organizationId, media.Id, fileName, image.ContentType, image.Bytes, image.Sha256, provenance, actor, media.CreatedAtUtc);
        await command.ExecuteNonQueryAsync(token);
        return media;
    }

    public async Task<MediaContent> GetMediaContentAsync(Guid organizationId, Guid mediaId, CancellationToken token)
    {
        await using var connection = await dataSource.OpenConnectionAsync(token);
        return await ReadMediaContentAsync(connection, null, organizationId, mediaId, token);
    }

    public async Task<IReadOnlyList<DraftSummary>> GetDraftsAsync(Guid organizationId, Guid? brandId, CancellationToken token)
    {
        await using var connection = await dataSource.OpenConnectionAsync(token);
        if (brandId is { } brand) await RequireBrandAsync(connection, null, organizationId, brand, token);
        await using var command = Command(connection, null,
            """
            SELECT d.id,d.brand_id,r.snapshot->>'title',d.current_revision,
                CASE WHEN d.active_approval_id IS NOT NULL THEN 'approved'
                     WHEN d.requested_review_revision=d.current_revision THEN 'in-review' ELSE 'draft' END,d.updated_at
            FROM preparation.drafts d
            JOIN preparation.draft_revisions r ON r.organization_id=d.organization_id AND r.draft_id=d.id AND r.revision=d.current_revision
            WHERE d.organization_id=$1 AND ($2::uuid IS NULL OR d.brand_id=$2)
            ORDER BY d.updated_at DESC,d.id
            """, organizationId, brandId);
        await using var reader = await command.ExecuteReaderAsync(token);
        var drafts = new List<DraftSummary>();
        while (await reader.ReadAsync(token))
            drafts.Add(new DraftSummary(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetInt32(3),
                reader.GetString(4), reader.GetFieldValue<DateTimeOffset>(5)));
        return drafts;
    }

    public async Task<Draft> GetDraftAsync(Guid organizationId, Guid draftId, CancellationToken token)
    {
        await using var connection = await dataSource.OpenConnectionAsync(token);
        return await ReadDraftAsync(connection, null, organizationId, draftId, lockHead: false, token);
    }

    public async Task<Draft> SaveDraftAsync(Guid organizationId, string actor, Guid? draftId, int? expectedRevision,
        DraftInput input, CancellationToken token)
    {
        await using var connection = await dataSource.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);
        var id = draftId ?? Guid.NewGuid();
        var revision = 1;
        if (draftId is not null)
        {
            var current = await ReadDraftAsync(connection, transaction, organizationId, id, lockHead: true, token);
            RequireRevision(current, expectedRevision ?? 0);
            revision = checked(current.Revision + 1);
        }
        await RequireBrandAsync(connection, transaction, organizationId, input.BrandId, token);
        var media = new List<MediaAsset>();
        long total = 0;
        foreach (var mediaId in input.MediaIds)
        {
            var item = await ReadMediaContentAsync(connection, transaction, organizationId, mediaId, token);
            media.Add(item.Asset);
            total += item.Asset.ByteLength;
        }
        if (total > 25L * 1024 * 1024) throw new PreparationException("too-large", "Selected media must total at most 25 MiB.");
        var now = time.GetUtcNow();
        if (draftId is null)
        {
            await using var insert = Command(connection, transaction,
                "INSERT INTO preparation.drafts(organization_id,id,brand_id,current_revision,updated_at) VALUES($1,$2,$3,1,$4)",
                organizationId, id, input.BrandId, now);
            await insert.ExecuteNonQueryAsync(token);
        }
        var content = new DraftContent(input.BrandId, input.Title, input.Caption, input.Language, input.Destination, input.SourceNotes, media);
        await using (var insert = Command(connection, transaction,
            "INSERT INTO preparation.draft_revisions(organization_id,draft_id,brand_id,revision,snapshot,created_by,created_at) VALUES($1,$2,$3,$4,$5::jsonb,$6,$7)",
            organizationId, id, input.BrandId, revision, JsonSerializer.Serialize(content, StoredJsonConstants.Default), actor, now))
            await insert.ExecuteNonQueryAsync(token);
        for (var position = 0; position < input.MediaIds.Length; position++)
        {
            await using var insert = Command(connection, transaction,
                "INSERT INTO preparation.draft_media(organization_id,draft_id,revision,position,media_id) VALUES($1,$2,$3,$4,$5)",
                organizationId, id, revision, position, input.MediaIds[position]);
            await insert.ExecuteNonQueryAsync(token);
        }
        await using (var update = Command(connection, transaction,
            "UPDATE preparation.drafts SET brand_id=$3,current_revision=$4,requested_review_revision=NULL,active_approval_id=NULL,updated_at=$5 WHERE organization_id=$1 AND id=$2",
            organizationId, id, input.BrandId, revision, now))
            await update.ExecuteNonQueryAsync(token);
        var result = await ReadDraftAsync(connection, transaction, organizationId, id, lockHead: false, token);
        await transaction.CommitAsync(token);
        return result;
    }

    public async Task<Draft> ReviewAsync(Guid organizationId, string actor, Guid draftId, int expectedRevision,
        ReviewAction action, string? reason, CancellationToken token)
    {
        await using var connection = await dataSource.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);
        var draft = await ReadDraftAsync(connection, transaction, organizationId, draftId, lockHead: true, token);
        RequireRevision(draft, expectedRevision);
        var now = time.GetUtcNow();
        switch (action)
        {
            case ReviewAction.Request:
                if (draft.Approval is null)
                {
                    await using var request = Command(connection, transaction,
                        "UPDATE preparation.drafts SET requested_review_revision=current_revision,updated_at=$3 WHERE organization_id=$1 AND id=$2",
                        organizationId, draftId, now);
                    await request.ExecuteNonQueryAsync(token);
                }
                break;
            case ReviewAction.Approve:
                if (draft.RequestedReviewRevision != expectedRevision)
                    throw new PreparationException("conflict", "Request review for this revision before approving it.");
                if (draft.Approval is null)
                {
                    var approvalId = Guid.NewGuid();
                    await using (var approve = Command(connection, transaction,
                        "INSERT INTO preparation.approvals(organization_id,id,draft_id,revision,approved_by,approved_at) VALUES($1,$2,$3,$4,$5,$6)",
                        organizationId, approvalId, draftId, expectedRevision, actor, now))
                        await approve.ExecuteNonQueryAsync(token);
                    await using var update = Command(connection, transaction,
                        "UPDATE preparation.drafts SET active_approval_id=$3,updated_at=$4 WHERE organization_id=$1 AND id=$2",
                        organizationId, draftId, approvalId, now);
                    await update.ExecuteNonQueryAsync(token);
                }
                break;
            case ReviewAction.Revoke:
                if (draft.Approval is null) throw new PreparationException("conflict", "This revision has no active approval.");
                await using (var revoke = Command(connection, transaction,
                    "UPDATE preparation.approvals SET revoked_at=$3,revoked_by=$4,reason=$5 WHERE organization_id=$1 AND id=$2",
                    organizationId, draft.Approval.Id, now, actor, reason))
                    await revoke.ExecuteNonQueryAsync(token);
                await using (var update = Command(connection, transaction,
                    "UPDATE preparation.drafts SET active_approval_id=NULL,requested_review_revision=NULL,updated_at=$3 WHERE organization_id=$1 AND id=$2",
                    organizationId, draftId, now))
                    await update.ExecuteNonQueryAsync(token);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(action));
        }
        var result = await ReadDraftAsync(connection, transaction, organizationId, draftId, lockHead: false, token);
        await transaction.CommitAsync(token);
        return result;
    }

    public async Task<ManualExport> CreateExportAsync(Guid organizationId, string actor, Guid draftId,
        int expectedRevision, Guid idempotencyKey, CancellationToken token)
    {
        var requestHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"manual-content-package:v1:{organizationId:D}:{draftId:D}:{expectedRevision}")));
        await using var connection = await dataSource.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);
        // Serialize organization-scoped keys even when conflicting requests name different drafts.
        await using (var keyLock = Command(connection, transaction,
            "SELECT pg_advisory_xact_lock(hashtextextended($1,0))", $"preparation-export:{organizationId:D}:{idempotencyKey:D}"))
            await keyLock.ExecuteNonQueryAsync(token);
        await using (var existing = Command(connection, transaction,
            "SELECT id,draft_id,revision,created_at,request_hash FROM preparation.exports WHERE organization_id=$1 AND idempotency_key=$2",
            organizationId, idempotencyKey))
        await using (var reader = await existing.ExecuteReaderAsync(token))
        {
            if (await reader.ReadAsync(token))
            {
                if (reader.GetString(4) != requestHash)
                    throw new PreparationException("conflict", "The idempotency key was already used for different content.");
                // This is an existing prepared copy, not a new export after revocation.
                return Export(organizationId, reader.GetGuid(0), reader.GetGuid(1), reader.GetInt32(2), reader.GetFieldValue<DateTimeOffset>(3));
            }
        }
        var draft = await ReadDraftAsync(connection, transaction, organizationId, draftId, lockHead: true, token);
        RequireRevision(draft, expectedRevision);
        if (draft.Approval is null) throw new PreparationException("conflict", "Approve this exact revision before exporting it.");
        var media = new List<MediaContent>();
        foreach (var asset in draft.Media)
            media.Add(await ReadMediaContentAsync(connection, transaction, organizationId, asset.Id, token));
        var export = Export(organizationId, Guid.NewGuid(), draftId, expectedRevision, time.GetUtcNow());
        var bytes = packages.Build(new PackageSnapshot(organizationId, export, draft, media));
        await using (var insert = Command(connection, transaction,
            """
            INSERT INTO preparation.exports(organization_id,id,draft_id,revision,approval_id,idempotency_key,request_hash,bytes,sha256,created_by,created_at)
            VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11)
            """, organizationId, export.Id, draftId, expectedRevision, draft.Approval.Id, idempotencyKey, requestHash, bytes,
            Convert.ToHexStringLower(SHA256.HashData(bytes)), actor, export.CreatedAtUtc))
            await insert.ExecuteNonQueryAsync(token);
        await transaction.CommitAsync(token);
        return export;
    }

    public async Task<byte[]> GetExportContentAsync(Guid organizationId, Guid exportId, CancellationToken token)
    {
        await using var connection = await dataSource.OpenConnectionAsync(token);
        await using var command = Command(connection, null,
            "SELECT bytes FROM preparation.exports WHERE organization_id=$1 AND id=$2", organizationId, exportId);
        return await command.ExecuteScalarAsync(token) as byte[]
            ?? throw new PreparationException("not-found", "The export was not found.");
    }

    private static async Task<Draft> ReadDraftAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction,
        Guid organizationId, Guid draftId, bool lockHead, CancellationToken token)
    {
        if (lockHead)
        {
            // Lock only the head, then read its joined snapshot after any competing transaction commits.
            await using var head = Command(connection, transaction,
                "SELECT id FROM preparation.drafts WHERE organization_id=$1 AND id=$2 FOR UPDATE", organizationId, draftId);
            if (await head.ExecuteScalarAsync(token) is null)
                throw new PreparationException("not-found", "The draft was not found.");
        }
        await using var command = Command(connection, transaction,
            """
            SELECT d.brand_id,d.current_revision,d.requested_review_revision,d.updated_at,r.snapshot::text,
                   a.id,a.approved_at,a.approved_by
            FROM preparation.drafts d
            JOIN preparation.draft_revisions r ON r.organization_id=d.organization_id AND r.draft_id=d.id AND r.revision=d.current_revision
            LEFT JOIN preparation.approvals a ON a.organization_id=d.organization_id AND a.id=d.active_approval_id AND a.revoked_at IS NULL
            WHERE d.organization_id=$1 AND d.id=$2
            """, organizationId, draftId);
        await using var reader = await command.ExecuteReaderAsync(token);
        if (!await reader.ReadAsync(token)) throw new PreparationException("not-found", "The draft was not found.");
        var revision = reader.GetInt32(1);
        int? requested = reader.IsDBNull(2) ? null : reader.GetInt32(2);
        var content = JsonSerializer.Deserialize<DraftContent>(reader.GetString(4), StoredJsonConstants.Default)
            ?? throw new InvalidDataException("Stored draft content is missing.");
        var approval = reader.IsDBNull(5) ? null : new DraftApproval(reader.GetGuid(5), revision,
            reader.GetFieldValue<DateTimeOffset>(6), reader.GetString(7));
        return new Draft(draftId, reader.GetGuid(0), content.Title, revision,
            approval is not null ? "approved" : requested == revision ? "in-review" : "draft",
            reader.GetFieldValue<DateTimeOffset>(3), content.Caption, content.Language, content.Destination,
            content.SourceNotes, content.Media, requested, approval);
    }

    private static async Task<MediaContent> ReadMediaContentAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction,
        Guid organizationId, Guid mediaId, CancellationToken token)
    {
        await using var command = Command(connection, transaction,
            "SELECT id,file_name,content_type,octet_length(bytes)::bigint,sha256,provenance,created_at,bytes FROM preparation.media WHERE organization_id=$1 AND id=$2",
            organizationId, mediaId);
        await using var reader = await command.ExecuteReaderAsync(token);
        if (!await reader.ReadAsync(token)) throw new PreparationException("not-found", "The image was not found.");
        return new MediaContent(ReadMedia(reader, organizationId), reader.GetFieldValue<byte[]>(7));
    }

    private static MediaAsset ReadMedia(NpgsqlDataReader reader, Guid organizationId) =>
        new(reader.GetGuid(0), organizationId, reader.GetString(1), reader.GetString(2), reader.GetInt64(3),
            reader.GetString(4), reader.GetString(5), reader.GetFieldValue<DateTimeOffset>(6));

    private static async Task RequireBrandAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction,
        Guid organizationId, Guid brandId, CancellationToken token)
    {
        await using var command = Command(connection, transaction,
            "SELECT 1 FROM preparation.brands WHERE organization_id=$1 AND id=$2", organizationId, brandId);
        if (await command.ExecuteScalarAsync(token) is null)
            throw new PreparationException("not-found", "The brand was not found.");
    }

    private static void RequireRevision(Draft draft, int expected)
    {
        if (draft.Revision != expected)
            throw new PreparationException("conflict", $"The draft changed. Current revision is {draft.Revision}; preserve your work and reload.");
    }

    private static ManualExport Export(Guid organizationId, Guid id, Guid draftId, int revision, DateTimeOffset createdAt) =>
        new(id, draftId, revision, createdAt, $"/api/organizations/{organizationId:D}/exports/{id:D}/download");

    private static NpgsqlCommand Command(NpgsqlConnection connection, NpgsqlTransaction? transaction, string sql, params object?[] values)
    {
        var command = new NpgsqlCommand(sql, connection, transaction);
        foreach (var value in values)
        {
            if (value is null) command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Unknown, Value = DBNull.Value });
            else command.Parameters.AddWithValue(value);
        }
        return command;
    }
}
