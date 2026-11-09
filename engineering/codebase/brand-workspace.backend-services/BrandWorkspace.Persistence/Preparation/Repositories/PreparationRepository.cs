using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BrandWorkspace.Application.Files.Constants;
using BrandWorkspace.Application.Files.Models;
using BrandWorkspace.Application.Files.Services;
using BrandWorkspace.Application.Foundation.Errors;
using BrandWorkspace.Application.Preparation.Models;
using BrandWorkspace.Application.Preparation.Repositories;
using BrandWorkspace.Application.Preparation.Services;
using BrandWorkspace.Domain.Preparation.Models;
using Dapper;
using WoW.Two.Sdk.Beta.Be.Data.Abstractions;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Foundation.Serialization;

namespace BrandWorkspace.Persistence.Preparation.Repositories;

/// <summary>
/// Accesses organization-qualified brands, images, drafts and exports, with serialized draft transitions. With WoW2
/// Files, images and export ZIPs keep their bytes there (planning/files-adoption.md #13): a row stores the file id, and
/// a row stored before Files reads its bytes until the boot backfill records its moved file in <c>media_files</c> or
/// <c>export_files</c>, since both tables reject updates.
/// </summary>
public sealed class PreparationRepository(
    IDbConnectionFactory connectionFactory,
    TimeProvider time,
    IManualPackageBuilder packages,
    IDeskFileStore files) : IPreparationRepository
{
    /// <summary>The metadata columns of <c>preparation.media m</c>, with its size when Files holds the bytes.</summary>
    private const string MediaColumns =
        "m.id,m.file_name,m.content_type,COALESCE(octet_length(m.bytes)::bigint,m.byte_length) AS byte_length,"
        + "m.sha256,m.provenance,m.created_at";

    /// <summary>Joins the file id recorded for a media row stored before Files.</summary>
    private const string MediaFileJoin =
        "LEFT JOIN preparation.media_files f ON f.organization_id=m.organization_id AND f.media_id=m.id";

    private const string ImageMissing = "The image was not found.";

    private const string DraftMissing = "The draft was not found.";

    public async Task<IReadOnlyList<Brand>> GetBrandsAsync(Guid organizationId, CancellationToken token)
    {
        const string sql =
            "SELECT id,name,time_zone FROM preparation.brands WHERE organization_id=@organizationId ORDER BY name,id";
        await using var connection = await connectionFactory.CreateOpenAsync(token);
        IEnumerable<BrandRow> rows = await connection.QueryAsync<BrandRow>(
            Command(sql, new { organizationId }, null, token));
        return rows.Select(row => new Brand(row.Id, organizationId, row.Name, row.TimeZone)).ToList();
    }

    public async Task<Brand> CreateBrandAsync(
        Guid organizationId,
        string name,
        string timeZone,
        CancellationToken token)
    {
        const string sql =
            """
            INSERT INTO preparation.brands(organization_id,id,name,time_zone)
            VALUES(@organizationId,@id,@name,@timeZone)
            """;
        var brand = new Brand(Guid.NewGuid(), organizationId, name, timeZone);
        await using var connection = await connectionFactory.CreateOpenAsync(token);
        await connection.ExecuteAsync(Command(sql, new { organizationId, id = brand.Id, name, timeZone }, null, token));
        return brand;
    }

    public async Task<IReadOnlyList<MediaAsset>> GetMediaAsync(Guid organizationId, CancellationToken token)
    {
        const string sql =
            $"""
             SELECT {MediaColumns} FROM preparation.media m
             WHERE m.organization_id=@organizationId
             ORDER BY m.created_at DESC,m.id
             """;
        await using var connection = await connectionFactory.CreateOpenAsync(token);
        IEnumerable<MediaRow> rows = await connection.QueryAsync<MediaRow>(
            Command(sql, new { organizationId }, null, token));
        return rows.Select(row => row.ToAsset(organizationId)).ToList();
    }

    public async Task<MediaAsset> CreateMediaAsync(
        Guid organizationId,
        string actor,
        string fileName,
        string provenance,
        ValidatedImage image,
        CancellationToken token)
    {
        const string sql =
            """
            INSERT INTO preparation.media(
                organization_id,id,file_name,content_type,bytes,sha256,provenance,created_by,created_at,file_id,
                byte_length)
            VALUES(@organizationId,@id,@fileName,@contentType,@bytes::bytea,@sha256,@provenance,@actor,@createdAt,
                @fileId::text,@byteLength::bigint)
            """;
        var media = new MediaAsset(
            Guid.NewGuid(),
            organizationId,
            fileName,
            image.ContentType,
            image.Bytes.LongLength,
            image.Sha256,
            provenance,
            time.GetUtcNow());
        // With Files the original lands there first, under the row's own reference; a refused insert leaves it
        // referenced.
        var file = files.IsEnabled
            ? await files.StoreAsync(
                image.Bytes,
                DeskFiles.Name(image.Sha256, image.ContentType),
                image.ContentType,
                DeskFileReference.PreparationMedia(organizationId, media.Id),
                token)
            : null;
        var parameters = new
        {
            organizationId,
            id = media.Id,
            fileName,
            contentType = image.ContentType,
            bytes = file is null ? image.Bytes : null,
            sha256 = image.Sha256,
            provenance,
            actor,
            createdAt = media.CreatedAtUtc,
            fileId = file?.Id,
            byteLength = file is null ? (long?)null : image.Bytes.LongLength,
        };
        await using var connection = await connectionFactory.CreateOpenAsync(token);
        await connection.ExecuteAsync(Command(sql, parameters, null, token));
        return media;
    }

    public async Task<Result<MediaContent>> GetMediaContentAsync(
        Guid organizationId,
        Guid mediaId,
        CancellationToken token)
    {
        await using var connection = await connectionFactory.CreateOpenAsync(token);
        return await ReadMediaContentAsync(connection, null, organizationId, mediaId, token);
    }

    public async Task<Result<PreparationMediaFile>> GetMediaFileAsync(
        Guid organizationId,
        Guid mediaId,
        CancellationToken token)
    {
        const string sql =
            $"""
             SELECT {MediaColumns},COALESCE(m.file_id,f.file_id) AS file_id
             FROM preparation.media m {MediaFileJoin}
             WHERE m.organization_id=@organizationId AND m.id=@mediaId
             """;
        await using var connection = await connectionFactory.CreateOpenAsync(token);
        MediaRow? row = await connection.QueryFirstOrDefaultAsync<MediaRow>(
            Command(sql, new { organizationId, mediaId }, null, token));
        if (row is null) return Result<PreparationMediaFile>.Fail(DeskErrors.NotFound(ImageMissing));
        return Result<PreparationMediaFile>.Ok(new PreparationMediaFile(row.ToAsset(organizationId), row.FileId));
    }

    public async Task<Result<IReadOnlyList<DraftSummary>>> GetDraftsAsync(
        Guid organizationId,
        Guid? brandId,
        CancellationToken token)
    {
        const string sql =
            """
            SELECT d.id,d.brand_id,r.snapshot->>'title' AS title,d.current_revision,
                CASE WHEN d.active_approval_id IS NOT NULL THEN 'approved'
                     WHEN d.requested_review_revision=d.current_revision THEN 'in-review' ELSE 'draft' END AS status,
                d.updated_at
            FROM preparation.drafts d
            JOIN preparation.draft_revisions r
                ON r.organization_id=d.organization_id AND r.draft_id=d.id AND r.revision=d.current_revision
            WHERE d.organization_id=@organizationId AND (@brandId::uuid IS NULL OR d.brand_id=@brandId)
            ORDER BY d.updated_at DESC,d.id
            """;
        await using var connection = await connectionFactory.CreateOpenAsync(token);
        if (brandId is { } brand
            && await BrandMissingAsync(connection, null, organizationId, brand, token) is { } missing)
            return Result<IReadOnlyList<DraftSummary>>.Fail(missing);
        IEnumerable<DraftSummaryRow> rows = await connection.QueryAsync<DraftSummaryRow>(
            Command(sql, new { organizationId, brandId }, null, token));
        List<DraftSummary> drafts = rows
            .Select(row => new DraftSummary(
                row.Id,
                row.BrandId,
                row.Title,
                row.CurrentRevision,
                row.Status,
                row.UpdatedAt))
            .ToList();
        return Result<IReadOnlyList<DraftSummary>>.Ok(drafts);
    }

    public async Task<Result<Draft>> GetDraftAsync(Guid organizationId, Guid draftId, CancellationToken token)
    {
        await using var connection = await connectionFactory.CreateOpenAsync(token);
        return await ReadDraftAsync(connection, null, organizationId, draftId, lockHead: false, token);
    }

    public async Task<Result<Draft>> SaveDraftAsync(
        Guid organizationId,
        string actor,
        Guid? draftId,
        int? expectedRevision,
        DraftInput input,
        CancellationToken token)
    {
        const string draftSql =
            """
            INSERT INTO preparation.drafts(organization_id,id,brand_id,current_revision,updated_at)
            VALUES(@organizationId,@draftId,@brandId,1,@now)
            """;
        const string revisionSql =
            """
            INSERT INTO preparation.draft_revisions(
                organization_id,draft_id,brand_id,revision,snapshot,created_by,created_at)
            VALUES(@organizationId,@draftId,@brandId,@revision,@snapshot::jsonb,@actor,@now)
            """;
        const string mediaSql =
            """
            INSERT INTO preparation.draft_media(organization_id,draft_id,revision,position,media_id)
            VALUES(@organizationId,@draftId,@revision,@position,@mediaId)
            """;
        const string headSql =
            """
            UPDATE preparation.drafts
            SET brand_id=@brandId,current_revision=@revision,requested_review_revision=NULL,active_approval_id=NULL,
                updated_at=@now
            WHERE organization_id=@organizationId AND id=@draftId
            """;
        await using var connection = await connectionFactory.CreateOpenAsync(token);
        await using DbTransaction transaction = await connection.BeginTransactionAsync(token);
        var id = draftId ?? Guid.NewGuid();
        var revision = 1;
        if (draftId is not null)
        {
            Result<Draft> read = await ReadDraftAsync(
                connection,
                transaction,
                organizationId,
                id,
                lockHead: true,
                token);
            if (read.IsFailure(out AppError? error, out var current)) return Result<Draft>.Fail(error);
            if (RevisionConflict(current, expectedRevision ?? 0) is { } conflict) return Result<Draft>.Fail(conflict);
            revision = checked(current.Revision + 1);
        }
        if (await BrandMissingAsync(connection, transaction, organizationId, input.BrandId, token) is { } missing)
            return Result<Draft>.Fail(missing);
        var media = new List<MediaAsset>();
        long total = 0;
        foreach (var mediaId in input.MediaIds)
        {
            if (await ReadMediaAsync(connection, transaction, organizationId, mediaId, token) is not { } item)
                return Result<Draft>.Fail(DeskErrors.NotFound(ImageMissing));
            media.Add(item);
            total += item.ByteLength;
        }
        if (total > 25L * 1024 * 1024)
            return Result<Draft>.Fail(DeskErrors.TooLarge("Selected media must total at most 25 MiB."));
        var now = time.GetUtcNow();
        var head = new { organizationId, draftId = id, brandId = input.BrandId, revision, now };
        if (draftId is null)
            await connection.ExecuteAsync(Command(draftSql, head, transaction, token));
        var content = new DraftContent(
            input.BrandId,
            input.Title,
            input.Caption,
            input.Language,
            input.Destination,
            input.SourceNotes,
            media);
        var entry = new
        {
            organizationId,
            draftId = id,
            brandId = input.BrandId,
            revision,
            snapshot = JsonSerializer.Serialize(content, StoredJsonConstants.Default),
            actor,
            now,
        };
        await connection.ExecuteAsync(Command(revisionSql, entry, transaction, token));
        for (var position = 0; position < input.MediaIds.Length; position++)
        {
            var attached = new { organizationId, draftId = id, revision, position, mediaId = input.MediaIds[position] };
            await connection.ExecuteAsync(Command(mediaSql, attached, transaction, token));
        }
        await connection.ExecuteAsync(Command(headSql, head, transaction, token));
        Result<Draft> result = await ReadDraftAsync(
            connection,
            transaction,
            organizationId,
            id,
            lockHead: false,
            token);
        if (result.IsSuccess) await transaction.CommitAsync(token);
        return result;
    }

    public async Task<Result<Draft>> ReviewAsync(
        Guid organizationId,
        string actor,
        Guid draftId,
        int expectedRevision,
        ReviewAction action,
        string? reason,
        CancellationToken token)
    {
        const string requestSql =
            """
            UPDATE preparation.drafts SET requested_review_revision=current_revision,updated_at=@now
            WHERE organization_id=@organizationId AND id=@draftId
            """;
        const string approveSql =
            """
            INSERT INTO preparation.approvals(organization_id,id,draft_id,revision,approved_by,approved_at)
            VALUES(@organizationId,@approvalId,@draftId,@revision,@actor,@now)
            """;
        const string activateSql =
            """
            UPDATE preparation.drafts SET active_approval_id=@approvalId,updated_at=@now
            WHERE organization_id=@organizationId AND id=@draftId
            """;
        const string revokeSql =
            """
            UPDATE preparation.approvals SET revoked_at=@now,revoked_by=@actor,reason=@reason
            WHERE organization_id=@organizationId AND id=@approvalId
            """;
        const string releaseSql =
            """
            UPDATE preparation.drafts SET active_approval_id=NULL,requested_review_revision=NULL,updated_at=@now
            WHERE organization_id=@organizationId AND id=@draftId
            """;
        await using var connection = await connectionFactory.CreateOpenAsync(token);
        await using DbTransaction transaction = await connection.BeginTransactionAsync(token);
        Result<Draft> read = await ReadDraftAsync(
            connection,
            transaction,
            organizationId,
            draftId,
            lockHead: true,
            token);
        if (read.IsFailure(out AppError? error, out var draft)) return Result<Draft>.Fail(error);
        if (RevisionConflict(draft, expectedRevision) is { } conflict) return Result<Draft>.Fail(conflict);
        var now = time.GetUtcNow();
        switch (action)
        {
            case ReviewAction.Request:
                if (draft.Approval is null)
                {
                    await connection.ExecuteAsync(
                        Command(requestSql, new { organizationId, draftId, now }, transaction, token));
                }
                break;
            case ReviewAction.Approve:
                if (draft.RequestedReviewRevision != expectedRevision)
                {
                    return Result<Draft>.Fail(
                        DeskErrors.Conflict("Request review for this revision before approving it."));
                }
                if (draft.Approval is null)
                {
                    var approval = new
                    {
                        organizationId,
                        approvalId = Guid.NewGuid(),
                        draftId,
                        revision = expectedRevision,
                        actor,
                        now,
                    };
                    await connection.ExecuteAsync(Command(approveSql, approval, transaction, token));
                    await connection.ExecuteAsync(Command(activateSql, approval, transaction, token));
                }
                break;
            case ReviewAction.Revoke:
                if (draft.Approval is null)
                    return Result<Draft>.Fail(DeskErrors.Conflict("This revision has no active approval."));
                var revocation = new { organizationId, approvalId = draft.Approval.Id, now, actor, reason };
                await connection.ExecuteAsync(Command(revokeSql, revocation, transaction, token));
                await connection.ExecuteAsync(
                    Command(releaseSql, new { organizationId, draftId, now }, transaction, token));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(action));
        }
        Result<Draft> result = await ReadDraftAsync(
            connection,
            transaction,
            organizationId,
            draftId,
            lockHead: false,
            token);
        if (result.IsSuccess) await transaction.CommitAsync(token);
        return result;
    }

    public async Task<Result<ManualExport>> CreateExportAsync(
        Guid organizationId,
        string actor,
        Guid draftId,
        int expectedRevision,
        Guid idempotencyKey,
        CancellationToken token)
    {
        const string lockSql = "SELECT pg_advisory_xact_lock(hashtextextended(@lockKey,0))";
        const string existingSql =
            """
            SELECT id,draft_id,revision,created_at,request_hash FROM preparation.exports
            WHERE organization_id=@organizationId AND idempotency_key=@idempotencyKey
            """;
        const string insertSql =
            """
            INSERT INTO preparation.exports(
                organization_id,id,draft_id,revision,approval_id,idempotency_key,request_hash,bytes,sha256,created_by,
                created_at,file_id)
            VALUES(@organizationId,@exportId,@draftId,@revision,@approvalId,@idempotencyKey,@requestHash,@bytes::bytea,
                @sha256,@actor,@createdAt,@fileId::text)
            """;
        var requestHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"manual-content-package:v1:{organizationId:D}:{draftId:D}:{expectedRevision}")));
        await using var connection = await connectionFactory.CreateOpenAsync(token);
        await using DbTransaction transaction = await connection.BeginTransactionAsync(token);
        // Serialize organization-scoped keys even when conflicting requests name different drafts.
        var lockKey = $"preparation-export:{organizationId:D}:{idempotencyKey:D}";
        await connection.ExecuteAsync(Command(lockSql, new { lockKey }, transaction, token));
        ExportRow? existing = await connection.QueryFirstOrDefaultAsync<ExportRow>(
            Command(existingSql, new { organizationId, idempotencyKey }, transaction, token));
        if (existing is not null)
        {
            if (existing.RequestHash != requestHash)
            {
                return Result<ManualExport>.Fail(
                    DeskErrors.Conflict("The idempotency key was already used for different content."));
            }
            // This is an existing prepared copy, not a new export after revocation.
            var prepared = Export(organizationId, existing.Id, existing.DraftId, existing.Revision, existing.CreatedAt);
            return Result<ManualExport>.Ok(prepared);
        }
        Result<Draft> read = await ReadDraftAsync(
            connection,
            transaction,
            organizationId,
            draftId,
            lockHead: true,
            token);
        if (read.IsFailure(out AppError? error, out var draft)) return Result<ManualExport>.Fail(error);
        if (RevisionConflict(draft, expectedRevision) is { } conflict) return Result<ManualExport>.Fail(conflict);
        if (draft.Approval is null)
            return Result<ManualExport>.Fail(DeskErrors.Conflict("Approve this exact revision before exporting it."));
        var media = new List<MediaContent>();
        foreach (var asset in draft.Media)
        {
            Result<MediaContent> content = await ReadMediaContentAsync(
                connection,
                transaction,
                organizationId,
                asset.Id,
                token);
            if (content.IsFailure(out AppError? missing, out var item)) return Result<ManualExport>.Fail(missing);
            media.Add(item);
        }
        var export = Export(organizationId, Guid.NewGuid(), draftId, expectedRevision, time.GetUtcNow());
        var bytes = packages.Build(new PackageSnapshot(organizationId, export, draft, media));
        // A private file under the export's own reference. It uploads while the key and the draft head stay locked, so
        // the idempotency key and request hash keep deciding exactly as before; a rolled-back insert leaves it
        // referenced.
        var file = files.IsEnabled
            ? await files.StoreAsync(
                bytes,
                $"manual-content-{export.Id:D}.zip",
                "application/zip",
                DeskFileReference.PreparationExport(organizationId, export.Id),
                token)
            : null;
        var stored = new
        {
            organizationId,
            exportId = export.Id,
            draftId,
            revision = expectedRevision,
            approvalId = draft.Approval.Id,
            idempotencyKey,
            requestHash,
            bytes = file is null ? bytes : null,
            sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)),
            actor,
            createdAt = export.CreatedAtUtc,
            fileId = file?.Id,
        };
        await connection.ExecuteAsync(Command(insertSql, stored, transaction, token));
        await transaction.CommitAsync(token);
        return Result<ManualExport>.Ok(export);
    }

    public async Task<Result<byte[]>> GetExportContentAsync(Guid organizationId, Guid exportId, CancellationToken token)
    {
        if (await ReadExportAsync(organizationId, exportId, token) is not { } export)
            return Result<byte[]>.Fail(ExportMissing());
        return Result<byte[]>.Ok(export.Bytes ?? await files.ReadAsync(export.FileId!, export.Sha256, token));
    }

    public async Task<Result<ExportFileLookup>> GetExportFileAsync(
        Guid organizationId,
        Guid exportId,
        CancellationToken token)
    {
        if (await ReadExportAsync(organizationId, exportId, token) is not { } export)
            return Result<ExportFileLookup>.Fail(ExportMissing());
        return Result<ExportFileLookup>.Ok(new ExportFileLookup(export.FileId));
    }

    /// <summary>Reads an export's stored bytes, or the Files file that holds them, and their SHA-256.</summary>
    private async Task<StoredExportRow?> ReadExportAsync(Guid organizationId, Guid exportId, CancellationToken token)
    {
        const string sql =
            """
            SELECT e.bytes,COALESCE(e.file_id,f.file_id) AS file_id,e.sha256 FROM preparation.exports e
            LEFT JOIN preparation.export_files f ON f.organization_id=e.organization_id AND f.export_id=e.id
            WHERE e.organization_id=@organizationId AND e.id=@exportId
            """;
        await using var connection = await connectionFactory.CreateOpenAsync(token);
        return await connection.QueryFirstOrDefaultAsync<StoredExportRow>(
            Command(sql, new { organizationId, exportId }, null, token));
    }

    private static async Task<Result<Draft>> ReadDraftAsync(
        DbConnection connection,
        DbTransaction? transaction,
        Guid organizationId,
        Guid draftId,
        bool lockHead,
        CancellationToken token)
    {
        const string headSql =
            "SELECT id FROM preparation.drafts WHERE organization_id=@organizationId AND id=@draftId FOR UPDATE";
        const string sql =
            """
            SELECT d.brand_id,d.current_revision,d.requested_review_revision,d.updated_at,r.snapshot::text,
                   a.id AS approval_id,a.approved_at,a.approved_by
            FROM preparation.drafts d
            JOIN preparation.draft_revisions r
                ON r.organization_id=d.organization_id AND r.draft_id=d.id AND r.revision=d.current_revision
            LEFT JOIN preparation.approvals a
                ON a.organization_id=d.organization_id AND a.id=d.active_approval_id AND a.revoked_at IS NULL
            WHERE d.organization_id=@organizationId AND d.id=@draftId
            """;
        if (lockHead)
        {
            // Lock only the head, then read its joined snapshot after any competing transaction commits.
            Guid? head = await connection.ExecuteScalarAsync<Guid?>(
                Command(headSql, new { organizationId, draftId }, transaction, token));
            if (head is null) return Result<Draft>.Fail(DeskErrors.NotFound(DraftMissing));
        }
        DraftRow? row = await connection.QueryFirstOrDefaultAsync<DraftRow>(
            Command(sql, new { organizationId, draftId }, transaction, token));
        if (row is null) return Result<Draft>.Fail(DeskErrors.NotFound(DraftMissing));
        var revision = row.CurrentRevision;
        int? requested = row.RequestedReviewRevision;
        var content = JsonSerializer.Deserialize<DraftContent>(row.Snapshot, StoredJsonConstants.Default)
            ?? throw new InvalidDataException("Stored draft content is missing.");
        var approval = row.ApprovalId is { } approvalId
            ? new DraftApproval(approvalId, revision, row.ApprovedAt!.Value, row.ApprovedBy!)
            : null;
        var draft = new Draft(
            draftId,
            row.BrandId,
            content.Title,
            revision,
            approval is not null ? "approved" : requested == revision ? "in-review" : "draft",
            row.UpdatedAt,
            content.Caption,
            content.Language,
            content.Destination,
            content.SourceNotes,
            content.Media,
            requested,
            approval);
        return Result<Draft>.Ok(draft);
    }

    /// <summary>
    /// Reads an image and its exact bytes: from PostgreSQL while it keeps them, else from WoW2 Files, checked against
    /// its SHA-256.
    /// </summary>
    private async Task<Result<MediaContent>> ReadMediaContentAsync(
        DbConnection connection,
        DbTransaction? transaction,
        Guid organizationId,
        Guid mediaId,
        CancellationToken token)
    {
        const string sql =
            $"""
             SELECT {MediaColumns},m.bytes,COALESCE(m.file_id,f.file_id) AS file_id
             FROM preparation.media m {MediaFileJoin}
             WHERE m.organization_id=@organizationId AND m.id=@mediaId
             """;
        MediaRow? row = await connection.QueryFirstOrDefaultAsync<MediaRow>(
            Command(sql, new { organizationId, mediaId }, transaction, token));
        if (row is null) return Result<MediaContent>.Fail(DeskErrors.NotFound(ImageMissing));
        var asset = row.ToAsset(organizationId);
        var bytes = row.Bytes ?? await files.ReadAsync(row.FileId!, asset.Sha256, token);
        return Result<MediaContent>.Ok(new MediaContent(asset, bytes));
    }

    /// <summary>Reads an image's metadata only; none when the organization lacks it.</summary>
    private static async Task<MediaAsset?> ReadMediaAsync(
        DbConnection connection,
        DbTransaction? transaction,
        Guid organizationId,
        Guid mediaId,
        CancellationToken token)
    {
        const string sql =
            $"SELECT {MediaColumns} FROM preparation.media m WHERE m.organization_id=@organizationId AND m.id=@mediaId";
        MediaRow? row = await connection.QueryFirstOrDefaultAsync<MediaRow>(
            Command(sql, new { organizationId, mediaId }, transaction, token));
        return row?.ToAsset(organizationId);
    }

    /// <summary>Finds the brand in the organization; an error when it is missing.</summary>
    private static async Task<AppError?> BrandMissingAsync(
        DbConnection connection,
        DbTransaction? transaction,
        Guid organizationId,
        Guid brandId,
        CancellationToken token)
    {
        const string sql = "SELECT 1 FROM preparation.brands WHERE organization_id=@organizationId AND id=@brandId";
        int? found = await connection.ExecuteScalarAsync<int?>(
            Command(sql, new { organizationId, brandId }, transaction, token));
        return found is null ? DeskErrors.NotFound("The brand was not found.") : null;
    }

    /// <summary>Refuses a change made against a revision other than the draft's current one.</summary>
    private static AppError? RevisionConflict(Draft draft, int expected)
    {
        if (draft.Revision == expected) return null;
        return DeskErrors.Conflict(
            $"The draft changed. Current revision is {draft.Revision}; preserve your work and reload.");
    }

    private static AppError ExportMissing()
    {
        return DeskErrors.NotFound("The export was not found.");
    }

    private static ManualExport Export(
        Guid organizationId,
        Guid id,
        Guid draftId,
        int revision,
        DateTimeOffset createdAt)
    {
        return new ManualExport(
            id,
            draftId,
            revision,
            createdAt,
            $"/api/organizations/{organizationId:D}/exports/{id:D}/download");
    }

    private static CommandDefinition Command(
        string sql,
        object parameters,
        DbTransaction? transaction,
        CancellationToken token)
    {
        return new CommandDefinition(sql, parameters, transaction, cancellationToken: token);
    }

    /// <summary>One brand of the organization.</summary>
    private sealed class BrandRow
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string TimeZone { get; init; } = string.Empty;
    }

    /// <summary>One image's metadata, with its bytes and Files file when the query reads them.</summary>
    private sealed class MediaRow
    {
        public Guid Id { get; init; }
        public string FileName { get; init; } = string.Empty;
        public string ContentType { get; init; } = string.Empty;
        public long ByteLength { get; init; }
        public string Sha256 { get; init; } = string.Empty;
        public string Provenance { get; init; } = string.Empty;
        public DateTimeOffset CreatedAt { get; init; }
        public byte[]? Bytes { get; init; }
        public string? FileId { get; init; }

        public MediaAsset ToAsset(Guid organizationId)
        {
            return new MediaAsset(Id, organizationId, FileName, ContentType, ByteLength, Sha256, Provenance, CreatedAt);
        }
    }

    /// <summary>One draft's current head in a list.</summary>
    private sealed class DraftSummaryRow
    {
        public Guid Id { get; init; }
        public Guid BrandId { get; init; }
        public string Title { get; init; } = string.Empty;
        public int CurrentRevision { get; init; }
        public string Status { get; init; } = string.Empty;
        public DateTimeOffset UpdatedAt { get; init; }
    }

    /// <summary>One draft's head, current snapshot and active approval.</summary>
    private sealed class DraftRow
    {
        public Guid BrandId { get; init; }
        public int CurrentRevision { get; init; }
        public int? RequestedReviewRevision { get; init; }
        public DateTimeOffset UpdatedAt { get; init; }
        public string Snapshot { get; init; } = string.Empty;
        public Guid? ApprovalId { get; init; }
        public DateTimeOffset? ApprovedAt { get; init; }
        public string? ApprovedBy { get; init; }
    }

    /// <summary>An export already prepared under an idempotency key.</summary>
    private sealed class ExportRow
    {
        public Guid Id { get; init; }
        public Guid DraftId { get; init; }
        public int Revision { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
        public string RequestHash { get; init; } = string.Empty;
    }

    /// <summary>An export's stored bytes, or the Files file holding them, and their SHA-256.</summary>
    private sealed class StoredExportRow
    {
        public byte[]? Bytes { get; init; }
        public string? FileId { get; init; }
        public string Sha256 { get; init; } = string.Empty;
    }
}
