using System.Data.Common;
using BrandWorkspace.Application.Foundation.Errors;
using BrandWorkspace.Application.Sourcing.Models;
using BrandWorkspace.Application.Sourcing.Repositories;
using BrandWorkspace.Domain.Sourcing.Models;
using Dapper;
using Npgsql;
using WoW.Two.Sdk.Beta.Be.Data.Abstractions;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace BrandWorkspace.Persistence.Sourcing.Repositories;

/// <summary>Accesses suppliers, their routes and people, and content-addressed media inside one organization.</summary>
public sealed class SourcingRepository(IDbConnectionFactory connectionFactory, TimeProvider time) : ISourcingRepository
{
    private const string SupplierColumns =
        "id, import_key, name, stage, country, city, website, notes, revision, created_at, updated_at";

    private const string PersonColumns = "id, supplier_id, name, role, notes, revision, created_at, updated_at";

    private const string RouteColumns =
        "id, supplier_id, person_id, channel, destination, label, source_url, verified, is_primary, notes, revision, "
        + "created_at, updated_at";

    private const string MediaColumns =
        "id, supplier_id, sha256, content_type, byte_size, width, height, has_thumbnail, capture, original_name, "
        + "caption, source_label, source_message_id, received_at, taken_at, has_location, revision, created_at, "
        + "updated_at";

    private const string DuplicateRoute = "This contact route already exists for the supplier.";

    public async Task<IReadOnlyList<SupplierSummary>> GetSuppliersAsync(
        Guid organizationId,
        string? query,
        string? stage,
        int limit,
        int offset,
        CancellationToken token)
    {
        const string sql =
            """
            SELECT s.id, s.import_key, s.name, s.stage, s.country, s.city, s.revision, s.updated_at,
                pr.channel AS primary_channel, pr.destination AS primary_destination,
                (SELECT count(*) FROM sourcing.contact_routes r
                    WHERE r.organization_id=s.organization_id AND r.supplier_id=s.id)::int AS route_count,
                (SELECT count(*) FROM sourcing.people p
                    WHERE p.organization_id=s.organization_id AND p.supplier_id=s.id)::int AS person_count,
                (SELECT count(*) FROM sourcing.media m
                    WHERE m.organization_id=s.organization_id AND m.supplier_id=s.id)::int AS media_count,
                (SELECT m.id FROM sourcing.media m
                    WHERE m.organization_id=s.organization_id AND m.supplier_id=s.id AND m.has_thumbnail
                    ORDER BY m.received_at DESC NULLS LAST, m.created_at DESC, m.id LIMIT 1) AS cover_media_id
            FROM sourcing.suppliers s
            LEFT JOIN sourcing.contact_routes pr
                ON pr.organization_id=s.organization_id AND pr.supplier_id=s.id AND pr.is_primary
            WHERE s.organization_id=@organizationId
                AND (@query::text IS NULL
                    OR s.name ILIKE @query ESCAPE '\' OR s.city ILIKE @query ESCAPE '\'
                    OR s.import_key ILIKE @query ESCAPE '\'
                    OR EXISTS (SELECT 1 FROM sourcing.contact_routes r
                        WHERE r.organization_id=s.organization_id AND r.supplier_id=s.id
                            AND r.destination ILIKE @query ESCAPE '\')
                    OR EXISTS (SELECT 1 FROM sourcing.people p
                        WHERE p.organization_id=s.organization_id AND p.supplier_id=s.id
                            AND p.name ILIKE @query ESCAPE '\'))
                AND (@stage::text IS NULL OR s.stage=@stage)
            ORDER BY lower(s.name), s.id
            LIMIT @limit OFFSET @offset
            """;
        var parameters = new
        {
            organizationId,
            query = query is null ? null : "%" + EscapeLike(query) + "%",
            stage,
            limit,
            offset,
        };
        await using var connection = await connectionFactory.CreateOpenAsync(token);
        IEnumerable<SupplierSummaryRow> rows = await connection.QueryAsync<SupplierSummaryRow>(
            Command(sql, parameters, null, token));
        return rows.Select(row => row.ToSummary()).ToList();
    }

    public async Task<Result<Supplier>> GetSupplierAsync(Guid organizationId, Guid supplierId, CancellationToken token)
    {
        await using var connection = await connectionFactory.CreateOpenAsync(token);
        Supplier? supplier = await ReadSupplierAsync(connection, null, organizationId, supplierId, token);
        return supplier is null ? Result<Supplier>.Fail(NotFound("supplier")) : Result<Supplier>.Ok(supplier);
    }

    public async Task<Supplier?> FindSupplierByKeyAsync(Guid organizationId, string key, CancellationToken token)
    {
        const string sql =
            "SELECT id FROM sourcing.suppliers WHERE organization_id=@organizationId AND import_key=@key";
        await using var connection = await connectionFactory.CreateOpenAsync(token);
        Guid? id = await connection.ExecuteScalarAsync<Guid?>(Command(sql, new { organizationId, key }, null, token));
        return id is { } supplierId
            ? await ReadSupplierAsync(connection, null, organizationId, supplierId, token)
            : null;
    }

    public async Task<Result<Supplier>> CreateSupplierAsync(
        Guid organizationId,
        string actor,
        string? key,
        SupplierInput input,
        CancellationToken token)
    {
        const string sql =
            """
            INSERT INTO sourcing.suppliers(organization_id, id, import_key, name, stage, country, city, website, notes,
                revision, created_by, created_at, updated_at)
            VALUES (@organizationId, @id, @key, @name, @stage, @country, @city, @website, @notes, 1, @actor, @now, @now)
            """;
        var id = Guid.NewGuid();
        var parameters = new
        {
            organizationId,
            id,
            key,
            name = input.Name,
            stage = input.Stage,
            country = input.Country,
            city = input.City,
            website = input.Website,
            notes = input.Notes,
            actor,
            now = time.GetUtcNow(),
        };
        await using var connection = await connectionFactory.CreateOpenAsync(token);
        try
        {
            await connection.ExecuteAsync(Command(sql, parameters, null, token));
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return Result<Supplier>.Fail(DeskErrors.Conflict("A supplier with this import key already exists."));
        }
        return Result<Supplier>.Ok((await ReadSupplierAsync(connection, null, organizationId, id, token))!);
    }

    public async Task<Result<Supplier>> UpdateSupplierAsync(
        Guid organizationId,
        Guid supplierId,
        int expectedRevision,
        SupplierInput input,
        CancellationToken token)
    {
        const string sql =
            """
            UPDATE sourcing.suppliers
            SET name=@name, stage=@stage, country=@country, city=@city, website=@website, notes=@notes,
                revision=revision+1, updated_at=@now
            WHERE organization_id=@organizationId AND id=@supplierId AND revision=@expectedRevision
            """;
        var parameters = new
        {
            organizationId,
            supplierId,
            expectedRevision,
            name = input.Name,
            stage = input.Stage,
            country = input.Country,
            city = input.City,
            website = input.Website,
            notes = input.Notes,
            now = time.GetUtcNow(),
        };
        await using var connection = await connectionFactory.CreateOpenAsync(token);
        if (await connection.ExecuteAsync(Command(sql, parameters, null, token)) == 0)
        {
            AppError error = await MissingOrConflictAsync(
                connection,
                null,
                "suppliers",
                organizationId,
                supplierId,
                "supplier",
                token);
            return Result<Supplier>.Fail(error);
        }
        return Result<Supplier>.Ok((await ReadSupplierAsync(connection, null, organizationId, supplierId, token))!);
    }

    public async Task<Result<SupplierPerson>> CreatePersonAsync(
        Guid organizationId,
        string actor,
        Guid supplierId,
        PersonInput input,
        CancellationToken token)
    {
        const string sql =
            $"""
             INSERT INTO sourcing.people(
                 organization_id, id, supplier_id, name, role, notes, revision, created_by, created_at, updated_at)
             VALUES (@organizationId, @id, @supplierId, @name, @role, @notes, 1, @actor, @now, @now)
             RETURNING {PersonColumns}
             """;
        var parameters = new
        {
            organizationId,
            id = Guid.NewGuid(),
            supplierId,
            name = input.Name,
            role = input.Role,
            notes = input.Notes,
            actor,
            now = time.GetUtcNow(),
        };
        await using var connection = await connectionFactory.CreateOpenAsync(token);
        await using DbTransaction transaction = await connection.BeginTransactionAsync(token);
        if (await SupplierMissingAsync(connection, transaction, organizationId, supplierId, token) is { } missing)
            return Result<SupplierPerson>.Fail(missing);
        PersonRow row = await connection.QueryFirstAsync<PersonRow>(Command(sql, parameters, transaction, token));
        await transaction.CommitAsync(token);
        return Result<SupplierPerson>.Ok(row.ToPerson());
    }

    public async Task<Result<SupplierPerson>> UpdatePersonAsync(
        Guid organizationId,
        Guid personId,
        int expectedRevision,
        PersonInput input,
        CancellationToken token)
    {
        const string sql =
            $"""
             UPDATE sourcing.people SET name=@name, role=@role, notes=@notes, revision=revision+1, updated_at=@now
             WHERE organization_id=@organizationId AND id=@personId AND revision=@expectedRevision
             RETURNING {PersonColumns}
             """;
        var parameters = new
        {
            organizationId,
            personId,
            expectedRevision,
            name = input.Name,
            role = input.Role,
            notes = input.Notes,
            now = time.GetUtcNow(),
        };
        await using var connection = await connectionFactory.CreateOpenAsync(token);
        PersonRow? row = await connection.QueryFirstOrDefaultAsync<PersonRow>(Command(sql, parameters, null, token));
        if (row is not null) return Result<SupplierPerson>.Ok(row.ToPerson());
        AppError error = await MissingOrConflictAsync(
            connection,
            null,
            "people",
            organizationId,
            personId,
            "person",
            token);
        return Result<SupplierPerson>.Fail(error);
    }

    public async Task<Result> DeletePersonAsync(
        Guid organizationId,
        Guid personId,
        int expectedRevision,
        CancellationToken token)
    {
        const string currentSql =
            "SELECT revision FROM sourcing.people WHERE organization_id=@organizationId AND id=@personId FOR UPDATE";
        const string releaseSql =
            """
            UPDATE sourcing.contact_routes SET person_id=NULL, revision=revision+1, updated_at=@now
            WHERE organization_id=@organizationId AND person_id=@personId
            """;
        const string deleteSql = "DELETE FROM sourcing.people WHERE organization_id=@organizationId AND id=@personId";
        await using var connection = await connectionFactory.CreateOpenAsync(token);
        await using DbTransaction transaction = await connection.BeginTransactionAsync(token);
        int? revision = await connection.ExecuteScalarAsync<int?>(
            Command(currentSql, new { organizationId, personId }, transaction, token));
        if (revision is null) return Result.Fail(NotFound("person"));
        if (revision != expectedRevision) return Result.Fail(Conflict("person"));
        // Routes stay with the supplier; they only lose the person reference, which counts as a change.
        var release = new { organizationId, personId, now = time.GetUtcNow() };
        await connection.ExecuteAsync(Command(releaseSql, release, transaction, token));
        await connection.ExecuteAsync(Command(deleteSql, new { organizationId, personId }, transaction, token));
        await transaction.CommitAsync(token);
        return Result.Ok();
    }

    public async Task<Result<ContactRoute>> CreateRouteAsync(
        Guid organizationId,
        string actor,
        Guid supplierId,
        RouteInput input,
        CancellationToken token)
    {
        const string sql =
            $"""
             INSERT INTO sourcing.contact_routes(organization_id, id, supplier_id, person_id, channel, destination,
                 label, source_url, verified, is_primary, notes, revision, created_by, created_at, updated_at)
             VALUES (@organizationId, @id, @supplierId, @personId::uuid, @channel, @destination, @label, @sourceUrl,
                 @verified, @isPrimary, @notes, 1, @actor, @now, @now)
             RETURNING {RouteColumns}
             """;
        DateTimeOffset now = time.GetUtcNow();
        await using var connection = await connectionFactory.CreateOpenAsync(token);
        await using DbTransaction transaction = await connection.BeginTransactionAsync(token);
        if (await SupplierMissingAsync(connection, transaction, organizationId, supplierId, token) is { } missing)
            return Result<ContactRoute>.Fail(missing);
        if (await PersonOutsideAsync(connection, transaction, organizationId, supplierId, input.PersonId, token)
            is { } outside)
            return Result<ContactRoute>.Fail(outside);
        if (input.IsPrimary)
            await ClearPrimaryAsync(connection, transaction, organizationId, supplierId, null, now, token);
        var parameters = new
        {
            organizationId,
            id = Guid.NewGuid(),
            supplierId,
            personId = input.PersonId,
            channel = input.Channel,
            destination = input.Destination,
            label = input.Label,
            sourceUrl = input.SourceUrl,
            verified = input.Verified,
            isPrimary = input.IsPrimary,
            notes = input.Notes,
            actor,
            now,
        };
        RouteRow row;
        try
        {
            row = await connection.QueryFirstAsync<RouteRow>(Command(sql, parameters, transaction, token));
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return Result<ContactRoute>.Fail(DeskErrors.Conflict(DuplicateRoute));
        }
        await transaction.CommitAsync(token);
        return Result<ContactRoute>.Ok(row.ToRoute());
    }

    public async Task<Result<ContactRoute>> UpdateRouteAsync(
        Guid organizationId,
        Guid routeId,
        int expectedRevision,
        RouteInput input,
        CancellationToken token)
    {
        const string ownerSql =
            "SELECT supplier_id FROM sourcing.contact_routes WHERE organization_id=@organizationId AND id=@routeId";
        const string currentSql =
            """
            SELECT revision FROM sourcing.contact_routes
            WHERE organization_id=@organizationId AND id=@routeId FOR UPDATE
            """;
        const string sql =
            $"""
             UPDATE sourcing.contact_routes
             SET person_id=@personId::uuid, channel=@channel, destination=@destination, label=@label,
                 source_url=@sourceUrl, verified=@verified, is_primary=@isPrimary, notes=@notes,
                 revision=revision+1, updated_at=@now
             WHERE organization_id=@organizationId AND id=@routeId
             RETURNING {RouteColumns}
             """;
        DateTimeOffset now = time.GetUtcNow();
        await using var connection = await connectionFactory.CreateOpenAsync(token);
        await using DbTransaction transaction = await connection.BeginTransactionAsync(token);
        Guid? owner = await connection.ExecuteScalarAsync<Guid?>(
            Command(ownerSql, new { organizationId, routeId }, transaction, token));
        if (owner is not { } supplierId) return Result<ContactRoute>.Fail(NotFound("contact route"));
        if (await SupplierMissingAsync(connection, transaction, organizationId, supplierId, token) is { } missing)
            return Result<ContactRoute>.Fail(missing);
        if (await PersonOutsideAsync(connection, transaction, organizationId, supplierId, input.PersonId, token)
            is { } outside)
            return Result<ContactRoute>.Fail(outside);
        int? revision = await connection.ExecuteScalarAsync<int?>(
            Command(currentSql, new { organizationId, routeId }, transaction, token));
        if (revision is null) return Result<ContactRoute>.Fail(NotFound("contact route"));
        if (revision != expectedRevision) return Result<ContactRoute>.Fail(Conflict("contact route"));
        if (input.IsPrimary)
            await ClearPrimaryAsync(connection, transaction, organizationId, supplierId, routeId, now, token);
        var parameters = new
        {
            organizationId,
            routeId,
            personId = input.PersonId,
            channel = input.Channel,
            destination = input.Destination,
            label = input.Label,
            sourceUrl = input.SourceUrl,
            verified = input.Verified,
            isPrimary = input.IsPrimary,
            notes = input.Notes,
            now,
        };
        RouteRow row;
        try
        {
            row = await connection.QueryFirstAsync<RouteRow>(Command(sql, parameters, transaction, token));
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return Result<ContactRoute>.Fail(DeskErrors.Conflict(DuplicateRoute));
        }
        await transaction.CommitAsync(token);
        return Result<ContactRoute>.Ok(row.ToRoute());
    }

    public async Task<Result> DeleteRouteAsync(
        Guid organizationId,
        Guid routeId,
        int expectedRevision,
        CancellationToken token)
    {
        const string sql =
            """
            DELETE FROM sourcing.contact_routes
            WHERE organization_id=@organizationId AND id=@routeId AND revision=@expectedRevision
            """;
        await using var connection = await connectionFactory.CreateOpenAsync(token);
        int deleted = await connection.ExecuteAsync(
            Command(sql, new { organizationId, routeId, expectedRevision }, null, token));
        if (deleted != 0) return Result.Ok();
        AppError error = await MissingOrConflictAsync(
            connection,
            null,
            "contact_routes",
            organizationId,
            routeId,
            "contact route",
            token);
        return Result.Fail(error);
    }

    public async Task<IReadOnlyList<SourcingMedia>> GetMediaAsync(
        Guid organizationId,
        Guid? supplierId,
        bool unassigned,
        int limit,
        int offset,
        CancellationToken token)
    {
        const string sql =
            $"""
             SELECT {MediaColumns}
             FROM sourcing.media
             WHERE organization_id=@organizationId
                 AND (@supplierId::uuid IS NULL OR supplier_id=@supplierId::uuid)
                 AND (NOT @unassigned OR supplier_id IS NULL)
             ORDER BY received_at DESC NULLS LAST, created_at DESC, id
             LIMIT @limit OFFSET @offset
             """;
        await using var connection = await connectionFactory.CreateOpenAsync(token);
        IEnumerable<MediaRow> rows = await connection.QueryAsync<MediaRow>(
            Command(sql, new { organizationId, supplierId, unassigned, limit, offset }, null, token));
        return rows.Select(row => row.ToMedia()).ToList();
    }

    public async Task<SourcingMediaTotals> GetMediaTotalsAsync(Guid organizationId, CancellationToken token)
    {
        const string sql =
            """
            SELECT count(*)::int AS total, (count(*) FILTER (WHERE supplier_id IS NULL))::int AS unassigned
            FROM sourcing.media WHERE organization_id=@organizationId
            """;
        await using var connection = await connectionFactory.CreateOpenAsync(token);
        TotalsRow row = await connection.QueryFirstAsync<TotalsRow>(Command(sql, new { organizationId }, null, token));
        return new SourcingMediaTotals(row.Total, row.Unassigned);
    }

    public async Task<SourcingMedia?> FindMediaBySha256Async(
        Guid organizationId,
        string sha256,
        CancellationToken token)
    {
        const string sql =
            $"SELECT {MediaColumns} FROM sourcing.media WHERE organization_id=@organizationId AND sha256=@sha256";
        await using var connection = await connectionFactory.CreateOpenAsync(token);
        MediaRow? row = await connection.QueryFirstOrDefaultAsync<MediaRow>(
            Command(sql, new { organizationId, sha256 }, null, token));
        return row?.ToMedia();
    }

    public async Task<IReadOnlyList<Guid>> FindNearDuplicatesAsync(
        Guid organizationId,
        long perceptualHash,
        int maximumDistance,
        Guid excludedMediaId,
        CancellationToken token)
    {
        const string sql =
            """
            SELECT id FROM sourcing.media
            WHERE organization_id=@organizationId AND perceptual_hash IS NOT NULL AND id<>@excludedMediaId
                AND bit_count(int8send(perceptual_hash # @perceptualHash)) <= @maximumDistance
            ORDER BY bit_count(int8send(perceptual_hash # @perceptualHash)), created_at, id
            LIMIT 20
            """;
        var parameters = new
        {
            organizationId,
            perceptualHash,
            excludedMediaId,
            maximumDistance = (long)maximumDistance,
        };
        await using var connection = await connectionFactory.CreateOpenAsync(token);
        IEnumerable<Guid> ids = await connection.QueryAsync<Guid>(Command(sql, parameters, null, token));
        return ids.ToList();
    }

    public async Task<Result<SourcingMediaUpload>> CreateMediaAsync(
        Guid organizationId,
        string actor,
        NewSourcingMedia media,
        string? fileId,
        CancellationToken token)
    {
        const string bytesSql =
            """
            INSERT INTO sourcing.media_contents(organization_id, sha256, bytes, thumbnail)
            VALUES (@organizationId, @sha256, @bytes, @thumbnail::bytea)
            ON CONFLICT (organization_id, sha256) DO NOTHING
            """;
        const string fileSql =
            """
            INSERT INTO sourcing.media_contents(organization_id, sha256, file_id)
            VALUES (@organizationId, @sha256, @fileId)
            ON CONFLICT (organization_id, sha256) DO UPDATE
                SET file_id = COALESCE(sourcing.media_contents.file_id, EXCLUDED.file_id)
            """;
        const string insertSql =
            $"""
             INSERT INTO sourcing.media(organization_id, id, supplier_id, sha256, content_type, byte_size, width,
                 height, has_thumbnail, capture, original_name, caption, source_label, source_message_id, received_at,
                 taken_at, has_location, perceptual_hash, revision, created_by, created_at, updated_at)
             VALUES (@organizationId, @id, @supplierId::uuid, @sha256, @contentType, @byteSize, @width::int,
                 @height::int, @hasThumbnail, @capture, @originalName, @caption, @sourceLabel, @sourceMessageId,
                 @receivedAt::timestamptz, @takenAt::timestamptz, @hasLocation, @perceptualHash::bigint, 1, @actor,
                 @now, @now)
             ON CONFLICT (organization_id, sha256) DO NOTHING
             RETURNING {MediaColumns}
             """;
        const string existingSql =
            $"SELECT {MediaColumns} FROM sourcing.media WHERE organization_id=@organizationId AND sha256=@sha256";
        DateTimeOffset now = time.GetUtcNow();
        InspectedMedia content = media.Content;
        await using var connection = await connectionFactory.CreateOpenAsync(token);
        await using DbTransaction transaction = await connection.BeginTransactionAsync(token);
        if (await SupplierOutsideAsync(connection, transaction, organizationId, media.SupplierId, token) is { } outside)
            return Result<SourcingMediaUpload>.Fail(outside);
        // With WoW2 Files the row keeps only the file id: the thumbnail is the original's 480 px variant
        // (files-adoption #10).
        if (fileId is null)
        {
            var stored = new
            {
                organizationId,
                sha256 = content.Sha256,
                bytes = content.Bytes,
                thumbnail = content.Thumbnail,
            };
            await connection.ExecuteAsync(Command(bytesSql, stored, transaction, token));
        }
        else
        {
            var stored = new { organizationId, sha256 = content.Sha256, fileId };
            await connection.ExecuteAsync(Command(fileSql, stored, transaction, token));
        }
        var parameters = new
        {
            organizationId,
            id = Guid.NewGuid(),
            supplierId = media.SupplierId,
            sha256 = content.Sha256,
            contentType = content.ContentType,
            byteSize = content.Bytes.Length,
            width = content.Width,
            height = content.Height,
            hasThumbnail = content.Thumbnail is not null,
            capture = media.Capture,
            originalName = media.OriginalName,
            caption = media.Caption,
            sourceLabel = media.SourceLabel,
            sourceMessageId = media.SourceMessageId,
            receivedAt = media.ReceivedAt,
            takenAt = content.TakenAt,
            hasLocation = content.HasLocation,
            perceptualHash = content.PerceptualHash,
            actor,
            now,
        };
        MediaRow? created = await connection.QueryFirstOrDefaultAsync<MediaRow>(
            Command(insertSql, parameters, transaction, token));
        if (created is not null)
        {
            await transaction.CommitAsync(token);
            return Result<SourcingMediaUpload>.Ok(new SourcingMediaUpload(created.ToMedia(), false, []));
        }
        MediaRow duplicate = await connection.QueryFirstAsync<MediaRow>(
            Command(existingSql, new { organizationId, sha256 = content.Sha256 }, transaction, token));
        await transaction.CommitAsync(token);
        return Result<SourcingMediaUpload>.Ok(new SourcingMediaUpload(duplicate.ToMedia(), true, []));
    }

    public async Task<Result<SourcingMedia>> UpdateMediaAsync(
        Guid organizationId,
        Guid mediaId,
        int expectedRevision,
        MediaUpdateInput input,
        CancellationToken token)
    {
        const string sql =
            $"""
             UPDATE sourcing.media
             SET supplier_id=@supplierId::uuid, capture=@capture, caption=@caption, revision=revision+1, updated_at=@now
             WHERE organization_id=@organizationId AND id=@mediaId AND revision=@expectedRevision
             RETURNING {MediaColumns}
             """;
        await using var connection = await connectionFactory.CreateOpenAsync(token);
        if (await SupplierOutsideAsync(connection, null, organizationId, input.SupplierId, token) is { } outside)
            return Result<SourcingMedia>.Fail(outside);
        var parameters = new
        {
            organizationId,
            mediaId,
            expectedRevision,
            supplierId = input.SupplierId,
            capture = input.Capture,
            caption = input.Caption,
            now = time.GetUtcNow(),
        };
        MediaRow? row = await connection.QueryFirstOrDefaultAsync<MediaRow>(Command(sql, parameters, null, token));
        if (row is not null) return Result<SourcingMedia>.Ok(row.ToMedia());
        AppError error = await MissingOrConflictAsync(
            connection,
            null,
            "media",
            organizationId,
            mediaId,
            "media item",
            token);
        return Result<SourcingMedia>.Fail(error);
    }

    public async Task<Result<int>> AssignMediaAsync(
        Guid organizationId,
        IReadOnlyList<Guid> mediaIds,
        Guid? supplierId,
        CancellationToken token)
    {
        const string sql =
            """
            UPDATE sourcing.media SET supplier_id=@supplierId::uuid, revision=revision+1, updated_at=@now
            WHERE organization_id=@organizationId AND id = ANY(@mediaIds)
                AND supplier_id IS DISTINCT FROM @supplierId::uuid
            """;
        await using var connection = await connectionFactory.CreateOpenAsync(token);
        if (await SupplierOutsideAsync(connection, null, organizationId, supplierId, token) is { } outside)
            return Result<int>.Fail(outside);
        var parameters = new { organizationId, mediaIds = mediaIds.ToArray(), supplierId, now = time.GetUtcNow() };
        return Result<int>.Ok(await connection.ExecuteAsync(Command(sql, parameters, null, token)));
    }

    public async Task<Result> DeleteMediaAsync(
        Guid organizationId,
        Guid mediaId,
        int expectedRevision,
        CancellationToken token)
    {
        const string deleteSql =
            """
            DELETE FROM sourcing.media
            WHERE organization_id=@organizationId AND id=@mediaId AND revision=@expectedRevision
            RETURNING sha256
            """;
        const string bytesSql =
            "DELETE FROM sourcing.media_contents WHERE organization_id=@organizationId AND sha256=@sha256";
        await using var connection = await connectionFactory.CreateOpenAsync(token);
        await using DbTransaction transaction = await connection.BeginTransactionAsync(token);
        string? sha256 = await connection.ExecuteScalarAsync<string?>(
            Command(deleteSql, new { organizationId, mediaId, expectedRevision }, transaction, token));
        if (sha256 is null)
        {
            AppError error = await MissingOrConflictAsync(
                connection,
                transaction,
                "media",
                organizationId,
                mediaId,
                "media item",
                token);
            return Result.Fail(error);
        }
        // SHA-256 is unique per organization, so the bytes are unreferenced once their media row is gone.
        await connection.ExecuteAsync(Command(bytesSql, new { organizationId, sha256 }, transaction, token));
        await transaction.CommitAsync(token);
        return Result.Ok();
    }

    public async Task<Result<SourcingMediaContent>> GetMediaContentAsync(
        Guid organizationId,
        Guid mediaId,
        CancellationToken token)
    {
        const string sql =
            """
            SELECT c.bytes, m.content_type, m.original_name, c.file_id
            FROM sourcing.media m
            JOIN sourcing.media_contents c ON c.organization_id=m.organization_id AND c.sha256=m.sha256
            WHERE m.organization_id=@organizationId AND m.id=@mediaId
            """;
        await using var connection = await connectionFactory.CreateOpenAsync(token);
        ContentRow? row = await connection.QueryFirstOrDefaultAsync<ContentRow>(
            Command(sql, new { organizationId, mediaId }, null, token));
        if (row is null) return Result<SourcingMediaContent>.Fail(NotFound("media item"));
        var content = new SourcingMediaContent(row.Bytes, row.ContentType, row.OriginalName, row.FileId);
        return Result<SourcingMediaContent>.Ok(content);
    }

    public async Task<Result<SourcingThumbnail>> GetMediaThumbnailAsync(
        Guid organizationId,
        Guid mediaId,
        CancellationToken token)
    {
        const string sql =
            """
            SELECT c.thumbnail, CASE WHEN m.has_thumbnail THEN c.file_id END AS file_id
            FROM sourcing.media m
            JOIN sourcing.media_contents c ON c.organization_id=m.organization_id AND c.sha256=m.sha256
            WHERE m.organization_id=@organizationId AND m.id=@mediaId
            """;
        await using var connection = await connectionFactory.CreateOpenAsync(token);
        ThumbnailRow? row = await connection.QueryFirstOrDefaultAsync<ThumbnailRow>(
            Command(sql, new { organizationId, mediaId }, null, token));
        if (row is null || (row.Thumbnail is null && row.FileId is null))
            return Result<SourcingThumbnail>.Fail(NotFound("thumbnail"));
        return Result<SourcingThumbnail>.Ok(new SourcingThumbnail(row.Thumbnail, row.FileId));
    }

    /// <summary>Reads a supplier with its people and routes; none when the organization lacks it.</summary>
    private static async Task<Supplier?> ReadSupplierAsync(
        DbConnection connection,
        DbTransaction? transaction,
        Guid organizationId,
        Guid supplierId,
        CancellationToken token)
    {
        const string supplierSql =
            $"""
             SELECT {SupplierColumns} FROM sourcing.suppliers
             WHERE organization_id=@organizationId AND id=@supplierId
             """;
        const string peopleSql =
            $"""
             SELECT {PersonColumns} FROM sourcing.people
             WHERE organization_id=@organizationId AND supplier_id=@supplierId ORDER BY lower(name), id
             """;
        const string routesSql =
            $"""
             SELECT {RouteColumns} FROM sourcing.contact_routes
             WHERE organization_id=@organizationId AND supplier_id=@supplierId
             ORDER BY is_primary DESC, channel, destination, id
             """;
        var parameters = new { organizationId, supplierId };
        SupplierRow? supplier = await connection.QueryFirstOrDefaultAsync<SupplierRow>(
            Command(supplierSql, parameters, transaction, token));
        if (supplier is null) return null;
        IEnumerable<PersonRow> people = await connection.QueryAsync<PersonRow>(
            Command(peopleSql, parameters, transaction, token));
        IEnumerable<RouteRow> routes = await connection.QueryAsync<RouteRow>(
            Command(routesSql, parameters, transaction, token));
        return new Supplier(
            supplier.Id,
            supplier.ImportKey,
            supplier.Name,
            supplier.Stage,
            supplier.Country,
            supplier.City,
            supplier.Website,
            supplier.Notes,
            people.Select(row => row.ToPerson()).ToList(),
            routes.Select(row => row.ToRoute()).ToList(),
            supplier.Revision,
            supplier.CreatedAt,
            supplier.UpdatedAt);
    }

    /// <summary>Locks the supplier for the transaction; an error when the organization lacks it.</summary>
    private static async Task<AppError?> SupplierMissingAsync(
        DbConnection connection,
        DbTransaction transaction,
        Guid organizationId,
        Guid supplierId,
        CancellationToken token)
    {
        const string sql =
            "SELECT id FROM sourcing.suppliers WHERE organization_id=@organizationId AND id=@supplierId FOR UPDATE";
        Guid? id = await connection.ExecuteScalarAsync<Guid?>(
            Command(sql, new { organizationId, supplierId }, transaction, token));
        return id is null ? NotFound("supplier") : null;
    }

    /// <summary>Refuses a person who does not belong to the supplier.</summary>
    private static async Task<AppError?> PersonOutsideAsync(
        DbConnection connection,
        DbTransaction transaction,
        Guid organizationId,
        Guid supplierId,
        Guid? personId,
        CancellationToken token)
    {
        const string sql =
            """
            SELECT 1 FROM sourcing.people
            WHERE organization_id=@organizationId AND supplier_id=@supplierId AND id=@personId
            """;
        if (personId is null) return null;
        int? found = await connection.ExecuteScalarAsync<int?>(
            Command(sql, new { organizationId, supplierId, personId }, transaction, token));
        return found is null ? DeskErrors.Invalid("Choose a person who belongs to this supplier.") : null;
    }

    /// <summary>Refuses a supplier reference outside the organization.</summary>
    private static async Task<AppError?> SupplierOutsideAsync(
        DbConnection connection,
        DbTransaction? transaction,
        Guid organizationId,
        Guid? supplierId,
        CancellationToken token)
    {
        const string sql = "SELECT 1 FROM sourcing.suppliers WHERE organization_id=@organizationId AND id=@supplierId";
        if (supplierId is null) return null;
        int? found = await connection.ExecuteScalarAsync<int?>(
            Command(sql, new { organizationId, supplierId }, transaction, token));
        return found is null ? DeskErrors.Invalid("Choose a supplier from this organization.") : null;
    }

    private static async Task ClearPrimaryAsync(
        DbConnection connection,
        DbTransaction transaction,
        Guid organizationId,
        Guid supplierId,
        Guid? keptRouteId,
        DateTimeOffset now,
        CancellationToken token)
    {
        const string sql =
            """
            UPDATE sourcing.contact_routes SET is_primary=false, revision=revision+1, updated_at=@now
            WHERE organization_id=@organizationId AND supplier_id=@supplierId AND is_primary
                AND (@keptRouteId::uuid IS NULL OR id<>@keptRouteId::uuid)
            """;
        await connection.ExecuteAsync(
            Command(sql, new { organizationId, supplierId, keptRouteId, now }, transaction, token));
    }

    /// <summary>Tells a missing row from one that changed since it was loaded.</summary>
    private static async Task<AppError> MissingOrConflictAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string table,
        Guid organizationId,
        Guid id,
        string noun,
        CancellationToken token)
    {
        var sql = $"SELECT 1 FROM sourcing.{table} WHERE organization_id=@organizationId AND id=@id";
        int? found = await connection.ExecuteScalarAsync<int?>(
            Command(sql, new { organizationId, id }, transaction, token));
        return found is null ? NotFound(noun) : Conflict(noun);
    }

    private static string EscapeLike(string value)
    {
        return value.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");
    }

    private static AppError NotFound(string noun)
    {
        return DeskErrors.NotFound($"The {noun} was not found.");
    }

    private static AppError Conflict(string noun)
    {
        return DeskErrors.Conflict($"The {noun} changed since it was loaded. Reload it before saving again.");
    }

    private static CommandDefinition Command(
        string sql,
        object parameters,
        DbTransaction? transaction,
        CancellationToken token)
    {
        return new CommandDefinition(sql, parameters, transaction, cancellationToken: token);
    }

    /// <summary>One supplier in a list, with its primary route and counts.</summary>
    private sealed class SupplierSummaryRow
    {
        public Guid Id { get; init; }
        public string? ImportKey { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Stage { get; init; } = string.Empty;
        public string? Country { get; init; }
        public string? City { get; init; }
        public int Revision { get; init; }
        public DateTimeOffset UpdatedAt { get; init; }
        public string? PrimaryChannel { get; init; }
        public string? PrimaryDestination { get; init; }
        public int RouteCount { get; init; }
        public int PersonCount { get; init; }
        public int MediaCount { get; init; }
        public Guid? CoverMediaId { get; init; }

        public SupplierSummary ToSummary()
        {
            return new SupplierSummary(
                Id,
                ImportKey,
                Name,
                Stage,
                Country,
                City,
                PrimaryChannel is null ? null : new SupplierRouteSummary(PrimaryChannel, PrimaryDestination!),
                RouteCount,
                PersonCount,
                MediaCount,
                CoverMediaId,
                Revision,
                UpdatedAt);
        }
    }

    /// <summary>One supplier's own columns.</summary>
    private sealed class SupplierRow
    {
        public Guid Id { get; init; }
        public string? ImportKey { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Stage { get; init; } = string.Empty;
        public string? Country { get; init; }
        public string? City { get; init; }
        public string? Website { get; init; }
        public string Notes { get; init; } = string.Empty;
        public int Revision { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
        public DateTimeOffset UpdatedAt { get; init; }
    }

    /// <summary>One person at a supplier.</summary>
    private sealed class PersonRow
    {
        public Guid Id { get; init; }
        public Guid SupplierId { get; init; }
        public string Name { get; init; } = string.Empty;
        public string? Role { get; init; }
        public string Notes { get; init; } = string.Empty;
        public int Revision { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
        public DateTimeOffset UpdatedAt { get; init; }

        public SupplierPerson ToPerson()
        {
            return new SupplierPerson(Id, SupplierId, Name, Role, Notes, Revision, CreatedAt, UpdatedAt);
        }
    }

    /// <summary>One contact route of a supplier.</summary>
    private sealed class RouteRow
    {
        public Guid Id { get; init; }
        public Guid SupplierId { get; init; }
        public Guid? PersonId { get; init; }
        public string Channel { get; init; } = string.Empty;
        public string Destination { get; init; } = string.Empty;
        public string? Label { get; init; }
        public string? SourceUrl { get; init; }
        public bool Verified { get; init; }
        public bool IsPrimary { get; init; }
        public string Notes { get; init; } = string.Empty;
        public int Revision { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
        public DateTimeOffset UpdatedAt { get; init; }

        public ContactRoute ToRoute()
        {
            return new ContactRoute(
                Id,
                SupplierId,
                PersonId,
                Channel,
                Destination,
                Label,
                SourceUrl,
                Verified,
                IsPrimary,
                Notes,
                Revision,
                CreatedAt,
                UpdatedAt);
        }
    }

    /// <summary>One media item's metadata.</summary>
    private sealed class MediaRow
    {
        public Guid Id { get; init; }
        public Guid? SupplierId { get; init; }
        public string Sha256 { get; init; } = string.Empty;
        public string ContentType { get; init; } = string.Empty;
        public int ByteSize { get; init; }
        public int? Width { get; init; }
        public int? Height { get; init; }
        public bool HasThumbnail { get; init; }
        public string Capture { get; init; } = string.Empty;
        public string OriginalName { get; init; } = string.Empty;
        public string Caption { get; init; } = string.Empty;
        public string? SourceLabel { get; init; }
        public string? SourceMessageId { get; init; }
        public DateTimeOffset? ReceivedAt { get; init; }
        public DateTimeOffset? TakenAt { get; init; }
        public bool HasLocation { get; init; }
        public int Revision { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
        public DateTimeOffset UpdatedAt { get; init; }

        public SourcingMedia ToMedia()
        {
            return new SourcingMedia(
                Id,
                SupplierId,
                Sha256,
                ContentType,
                ByteSize,
                Width,
                Height,
                HasThumbnail,
                Capture,
                OriginalName,
                Caption,
                SourceLabel,
                SourceMessageId,
                ReceivedAt,
                TakenAt,
                HasLocation,
                Revision,
                CreatedAt,
                UpdatedAt);
        }
    }

    /// <summary>How many media items the organization holds, and how many await a supplier.</summary>
    private sealed class TotalsRow
    {
        public int Total { get; init; }
        public int Unassigned { get; init; }
    }

    /// <summary>One original's bytes, or the Files file holding them, with its type and name.</summary>
    private sealed class ContentRow
    {
        public byte[]? Bytes { get; init; }
        public string ContentType { get; init; } = string.Empty;
        public string OriginalName { get; init; } = string.Empty;
        public string? FileId { get; init; }
    }

    /// <summary>One thumbnail's stored bytes, or the Files file whose variant serves it.</summary>
    private sealed class ThumbnailRow
    {
        public byte[]? Thumbnail { get; init; }
        public string? FileId { get; init; }
    }
}
