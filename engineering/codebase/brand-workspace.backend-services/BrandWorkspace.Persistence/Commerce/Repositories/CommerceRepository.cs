using System.Data.Common;
using System.Security.Cryptography;
using System.Text.Json;
using BrandWorkspace.Application.Commerce.Models;
using BrandWorkspace.Application.Commerce.Repositories;
using BrandWorkspace.Application.Commerce.Services;
using BrandWorkspace.Application.Foundation.Errors;
using BrandWorkspace.Domain.Commerce.Models;
using BrandWorkspace.Persistence.Commerce.Serializers;
using Dapper;
using Npgsql;
using WoW.Two.Sdk.Beta.Be.Data.Abstractions;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace BrandWorkspace.Persistence.Commerce.Repositories;

/// <summary>Accesses brands' serialized commerce drafts and immutable releases within their organization.</summary>
public sealed class CommerceRepository(IDbConnectionFactory connectionFactory, TimeProvider time, ICommerceRules rules)
    : ICommerceRepository
{
    private const string ReleaseColumns =
        "id,brand_id,revision,source_revision,published_at,note,snapshot::text,withdrawn_at";

    private const int MaximumBlobBytes = 15 * 1024 * 1024;

    private const int MaximumEditionBlobBytes = 64 * 1024 * 1024;

    private static readonly JsonSerializerOptions Json = CommerceJson.Options;

    public async Task<Result<CommerceDraftLookup>> GetAsync(Guid organizationId, Guid brandId, CancellationToken token)
    {
        await using var connection = await connectionFactory.CreateOpenAsync(token);
        if (await BrandMissingAsync(connection, null, organizationId, brandId, false, token) is { } missing)
            return Result<CommerceDraftLookup>.Fail(missing);
        CommerceDraft? draft = await ReadAsync(connection, null, organizationId, brandId, token);
        return Result<CommerceDraftLookup>.Ok(new CommerceDraftLookup(draft));
    }

    public async Task<Result<CommerceDraft>> SaveAsync(
        Guid organizationId,
        string actor,
        Guid brandId,
        int expectedRevision,
        CommerceSnapshot snapshot,
        IReadOnlyList<CommerceBlob> blobs,
        bool owner,
        CancellationToken token,
        bool preserveImportedPrices = false)
    {
        const string slugSql =
            "SELECT public_slug FROM commerce.profiles WHERE organization_id=@organizationId AND brand_id=@brandId";
        if (snapshot is null) return Result<CommerceDraft>.Fail(DeskErrors.Invalid("Brand identity is required."));
        // Generic edits cannot create, replace, approve, or erase server-owned artifacts.
        snapshot = snapshot with { CatalogueEditions = [] };
        rules.Validate(snapshot);
        await using var connection = await connectionFactory.CreateOpenAsync(token);
        await using DbTransaction transaction = await connection.BeginTransactionAsync(token);
        if (await BrandMissingAsync(connection, transaction, organizationId, brandId, true, token) is { } missing)
            return Result<CommerceDraft>.Fail(missing);
        CommerceDraft? previous = await ReadAsync(connection, transaction, organizationId, brandId, token);
        if ((previous?.Revision ?? 0) != expectedRevision)
            return Result<CommerceDraft>.Fail(DeskErrors.Conflict("The draft changed. Reload before saving."));
        string? assigned = await connection.ExecuteScalarAsync<string?>(
            Command(slugSql, new { organizationId, brandId }, transaction, token));
        if (assigned is not null && assigned != snapshot.Slug)
            return Result<CommerceDraft>.Fail(DeskErrors.Conflict("The public slug is fixed after its first release."));
        snapshot = snapshot with
        {
            CatalogueEditions = previous?.Snapshot.CatalogueEditions ?? [],
            Products = snapshot.Products.Select(item =>
            {
                var old = previous?.Snapshot.Products.FirstOrDefault(p => p.Document.Id == item.Document.Id);
                return item with
                {
                    Translations = item.Translations ?? old?.Translations,
                    MediaGroups = item.MediaGroups ?? old?.MediaGroups
                };
            }).ToArray()
        };
        if (!preserveImportedPrices) rules.ValidateEditedPrices(snapshot, previous?.Snapshot);
        if (!owner) snapshot = rules.RestrictEditorApprovals(snapshot, previous?.Snapshot);
        snapshot = snapshot with
        {
            Products = snapshot.Products.Select(product =>
            {
                var old = previous?.Snapshot.Products.FirstOrDefault(x => x.Document.Id == product.Document.Id);
                var before = old is null
                    ? null
                    : JsonSerializer.Serialize(new { old.Document, old.Translations, old.MediaGroups }, Json);
                var after = JsonSerializer.Serialize(
                    new { product.Document, product.Translations, product.MediaGroups },
                    Json);
                var same = old is not null && before == after;
                return product with
                {
                    Revision = old is null ? 1 : same ? old.Revision : checked(old.Revision + 1),
                    SourceRevision = old?.SourceRevision ?? product.SourceRevision
                };
            }).ToArray()
        };
        Result<CommerceDraft> result = await WriteDraftAsync(
            connection,
            transaction,
            new DraftWrite(organizationId, actor, brandId, expectedRevision, snapshot),
            blobs,
            MaximumBlobBytes,
            token);
        if (result.IsSuccess) await transaction.CommitAsync(token);
        return result;
    }

    public async Task<Result<CommerceDraft>> AttachCatalogueEditionAsync(
        Guid organizationId,
        string actor,
        Guid brandId,
        int expectedRevision,
        CatalogueEdition edition,
        IReadOnlyList<CommerceBlob> blobs,
        CancellationToken token)
    {
        if (edition.ApprovedForPublication)
            return Invalid<CommerceDraft>("New catalogue editions require explicit owner approval.");
        await using var connection = await connectionFactory.CreateOpenAsync(token);
        await using DbTransaction transaction = await connection.BeginTransactionAsync(token);
        if (await BrandMissingAsync(connection, transaction, organizationId, brandId, true, token) is { } missing)
            return Result<CommerceDraft>.Fail(missing);
        if (await ReadAsync(connection, transaction, organizationId, brandId, token) is not { } draft)
            return Result<CommerceDraft>.Fail(DeskErrors.NotFound("Save the brand draft first."));
        if (draft.Revision != expectedRevision)
        {
            return Result<CommerceDraft>.Fail(
                DeskErrors.Conflict("The draft changed. Reload before attaching the edition."));
        }
        var pageProducts = edition.Pages.SelectMany(p => p.ProductIds);
        if (!pageProducts.All(id => draft.Snapshot.Products.Any(p => p.Document.Id == id)))
            return Invalid<CommerceDraft>("Catalogue pages must refer to products in this brand.");
        if (!rules.EditionEligible(edition, draft.Snapshot) && edition.IsActive)
            return Invalid<CommerceDraft>("Catalogue products changed approval. Prepare the catalogue again.");
        var expectedHashes = EditionHashes(edition).ToHashSet();
        if (!blobs.Select(b => b.Sha256).ToHashSet().SetEquals(expectedHashes))
            return Invalid<CommerceDraft>("Provide every catalogue artifact and no extra files.");
        var existing = draft.Snapshot.CatalogueEditions
            .FirstOrDefault(e => e.Locale == edition.Locale && e.Slug == edition.Slug);
        if (edition.Slug is not null && existing is not null && existing.SourceDesignId != edition.SourceDesignId)
        {
            return Invalid<CommerceDraft>(
                "This catalogue name and language already belong to another saved document. Choose a different slug.");
        }
        if (existing is not null
            && existing.SourceDesignId == edition.SourceDesignId
            && existing.SourceRevision == edition.SourceRevision
            && existing.PdfSha256 == edition.PdfSha256
            && JsonSerializer.Serialize(existing.Pages, Json) == JsonSerializer.Serialize(edition.Pages, Json)
            && JsonSerializer.Serialize(existing.Web, Json) == JsonSerializer.Serialize(edition.Web, Json)
            && existing.IsActive == edition.IsActive
            && existing.Description == edition.Description)
            return Result<CommerceDraft>.Ok(draft);
        var snapshot = draft.Snapshot with
        {
            CatalogueEditions = draft.Snapshot.CatalogueEditions
                .Where(e => (e.Locale != edition.Locale || e.Slug != edition.Slug)
                    && (edition.Slug is null || e.SourceDesignId != edition.SourceDesignId))
                .Append(edition)
                .ToArray()
        };
        Result<CommerceDraft> result = await WriteDraftAsync(
            connection,
            transaction,
            new DraftWrite(organizationId, actor, brandId, expectedRevision, snapshot),
            blobs,
            MaximumEditionBlobBytes,
            token);
        if (result.IsSuccess) await transaction.CommitAsync(token);
        return result;
    }

    public Task<Result<CommerceRelease>> PublishCatalogueEditionAsync(
        Guid organizationId,
        string actor,
        Guid brandId,
        int expectedRevision,
        string locale,
        string note,
        CancellationToken token)
    {
        return PublishEditionAsync(
            new DraftWrite(organizationId, actor, brandId, expectedRevision, null),
            locale,
            null,
            note,
            token);
    }

    public Task<Result<CommerceRelease>> PublishCatalogueEditionByIdAsync(
        Guid organizationId,
        string actor,
        Guid brandId,
        int expectedRevision,
        Guid editionId,
        string note,
        CancellationToken token)
    {
        return PublishEditionAsync(
            new DraftWrite(organizationId, actor, brandId, expectedRevision, null),
            null,
            editionId,
            note,
            token);
    }

    public async Task<Result<IReadOnlyList<CommerceRelease>>> ReleasesAsync(
        Guid organizationId,
        Guid brandId,
        CancellationToken token)
    {
        const string sql =
            $"""
             SELECT {ReleaseColumns} FROM commerce.releases
             WHERE organization_id=@organizationId AND brand_id=@brandId
             ORDER BY revision DESC
             """;
        await using var connection = await connectionFactory.CreateOpenAsync(token);
        if (await BrandMissingAsync(connection, null, organizationId, brandId, false, token) is { } missing)
            return Result<IReadOnlyList<CommerceRelease>>.Fail(missing);
        IEnumerable<ReleaseRow> rows = await connection.QueryAsync<ReleaseRow>(
            Command(sql, new { organizationId, brandId }, null, token));
        return Result<IReadOnlyList<CommerceRelease>>.Ok(rows.Select(row => row.ToRelease()).ToList());
    }

    public async Task<Result<CommerceRelease>> PublishAsync(
        Guid organizationId,
        string actor,
        Guid brandId,
        int expectedRevision,
        string note,
        CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(note) || note.Length > 1000)
            return Invalid<CommerceRelease>("Record the reason for publishing (maximum 1000 characters).");
        await using var connection = await connectionFactory.CreateOpenAsync(token);
        await using DbTransaction transaction = await connection.BeginTransactionAsync(token);
        if (await BrandMissingAsync(connection, transaction, organizationId, brandId, true, token) is { } missing)
            return Result<CommerceRelease>.Fail(missing);
        if (await ReadAsync(connection, transaction, organizationId, brandId, token) is not { } draft)
            return Result<CommerceRelease>.Fail(DeskErrors.NotFound("Save the brand draft first."));
        if (draft.Revision != expectedRevision)
            return Result<CommerceRelease>.Fail(DeskErrors.Conflict("The draft changed. Review the latest revision."));
        Result<CommerceRelease> release = await WriteReleaseAsync(
            connection,
            transaction,
            new DraftWrite(organizationId, actor, brandId, draft.Revision, draft.Snapshot),
            note,
            token);
        if (release.IsSuccess) await transaction.CommitAsync(token);
        return release;
    }

    public async Task<Result> WithdrawAsync(
        Guid organizationId,
        string actor,
        Guid brandId,
        Guid releaseId,
        string reason,
        CancellationToken token)
    {
        const string withdrawSql =
            """
            UPDATE commerce.releases SET withdrawn_at=@now,withdrawn_by=@actor,withdrawal_reason=@reason
            WHERE organization_id=@organizationId AND brand_id=@brandId AND id=@releaseId AND withdrawn_at IS NULL
            """;
        const string headSql =
            """
            UPDATE commerce.profiles SET active_release_id=NULL
            WHERE organization_id=@organizationId AND brand_id=@brandId AND active_release_id=@releaseId
            """;
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 1000)
            return Result.Fail(DeskErrors.Invalid("A withdrawal reason is required."));
        await using var connection = await connectionFactory.CreateOpenAsync(token);
        await using DbTransaction transaction = await connection.BeginTransactionAsync(token);
        if (await BrandMissingAsync(connection, transaction, organizationId, brandId, true, token) is { } missing)
            return Result.Fail(missing);
        var withdrawal = new { organizationId, brandId, releaseId, now = time.GetUtcNow(), actor, reason };
        if (await connection.ExecuteAsync(Command(withdrawSql, withdrawal, transaction, token)) != 1)
            return Result.Fail(DeskErrors.NotFound("Active release unavailable."));
        await connection.ExecuteAsync(Command(headSql, new { organizationId, brandId, releaseId }, transaction, token));
        await transaction.CommitAsync(token);
        return Result.Ok();
    }

    public async Task<CommerceRelease?> PublicAsync(string slug, Guid? releaseId, CancellationToken token)
    {
        var columns = string.Join(",", ReleaseColumns.Split(',').Select(column => "r." + column));
        var sql =
            $"""
             SELECT {columns} FROM commerce.releases r
             JOIN commerce.profiles p ON p.organization_id=r.organization_id AND p.brand_id=r.brand_id
             WHERE p.public_slug=@slug
                 AND r.id=COALESCE(@releaseId::uuid,p.active_release_id)
                 AND r.withdrawn_at IS NULL
             """;
        await using var connection = await connectionFactory.CreateOpenAsync(token);
        ReleaseRow? row = await connection.QueryFirstOrDefaultAsync<ReleaseRow>(
            Command(sql, new { slug, releaseId }, null, token));
        return row?.ToRelease();
    }

    public async Task<CommerceBlob?> BlobAsync(Guid brandId, string sha256, CancellationToken token)
    {
        const string sql =
            "SELECT content_type,bytes,file_id FROM commerce.media WHERE brand_id=@brandId AND sha256=@sha256";
        await using var connection = await connectionFactory.CreateOpenAsync(token);
        BlobRow? row = await connection.QueryFirstOrDefaultAsync<BlobRow>(
            Command(sql, new { brandId, sha256 }, null, token));
        return row is null ? null : new CommerceBlob(sha256, row.ContentType, row.Bytes ?? [], row.FileId);
    }

    public async Task<string?> MediaFileAsync(Guid brandId, string sha256, CancellationToken token)
    {
        const string sql = "SELECT file_id FROM commerce.media WHERE brand_id=@brandId AND sha256=@sha256";
        await using var connection = await connectionFactory.CreateOpenAsync(token);
        return await connection.ExecuteScalarAsync<string?>(Command(sql, new { brandId, sha256 }, null, token));
    }

    public async Task<Result<CreativeImport>> ImportCreativeAsync(
        Guid organizationId,
        string actor,
        CreativeImportInput input,
        CancellationToken token)
    {
        const string sql =
            """
            INSERT INTO commerce.creative_imports(organization_id,id,brand_id,actor,created_at,snapshot)
            VALUES(@organizationId,@id,@brandId,@actor,@createdAt,@snapshot::jsonb)
            """;
        await using var connection = await connectionFactory.CreateOpenAsync(token);
        if (await BrandMissingAsync(connection, null, organizationId, input.BrandId, false, token) is { } missing)
            return Result<CreativeImport>.Fail(missing);
        var result = new CreativeImport(Guid.NewGuid(), time.GetUtcNow(), "unreviewed", input);
        var parameters = new
        {
            organizationId,
            id = result.Id,
            brandId = input.BrandId,
            actor,
            createdAt = result.CreatedAt,
            snapshot = JsonSerializer.Serialize(input, Json),
        };
        await connection.ExecuteAsync(Command(sql, parameters, null, token));
        return Result<CreativeImport>.Ok(result);
    }

    public async Task<Result<IReadOnlyList<CreativeImport>>> CreativeImportsAsync(
        Guid organizationId,
        Guid brandId,
        CancellationToken token)
    {
        const string sql =
            """
            SELECT id,created_at,snapshot::text FROM commerce.creative_imports
            WHERE organization_id=@organizationId AND brand_id=@brandId
            ORDER BY created_at DESC LIMIT 100
            """;
        await using var connection = await connectionFactory.CreateOpenAsync(token);
        if (await BrandMissingAsync(connection, null, organizationId, brandId, false, token) is { } missing)
            return Result<IReadOnlyList<CreativeImport>>.Fail(missing);
        IEnumerable<CreativeImportRow> rows = await connection.QueryAsync<CreativeImportRow>(
            Command(sql, new { organizationId, brandId }, null, token));
        List<CreativeImport> imports = rows
            .Select(row => new CreativeImport(
                row.Id,
                row.CreatedAt,
                "unreviewed",
                JsonSerializer.Deserialize<CreativeImportInput>(row.Snapshot, Json)!))
            .ToList();
        return Result<IReadOnlyList<CreativeImport>>.Ok(imports);
    }

    /// <summary>Publishes a saved catalogue edition, first approving it when its owner has not yet.</summary>
    private async Task<Result<CommerceRelease>> PublishEditionAsync(
        DraftWrite target,
        string? locale,
        Guid? editionId,
        string note,
        CancellationToken token)
    {
        await using var connection = await connectionFactory.CreateOpenAsync(token);
        await using DbTransaction transaction = await connection.BeginTransactionAsync(token);
        if (await BrandMissingAsync(connection, transaction, target.OrganizationId, target.BrandId, true, token)
            is { } missing)
            return Result<CommerceRelease>.Fail(missing);
        if (await ReadAsync(connection, transaction, target.OrganizationId, target.BrandId, token) is not { } draft)
            return Result<CommerceRelease>.Fail(DeskErrors.NotFound("Save the brand draft first."));
        if (draft.Revision != target.ExpectedRevision)
            return Result<CommerceRelease>.Fail(DeskErrors.Conflict("The draft changed. Review the latest revision."));
        var edition = draft.Snapshot.CatalogueEditions.SingleOrDefault(e => editionId is not null
            ? e.Id == editionId
            : e.Locale == locale && e.Slug is null);
        if (edition is null)
        {
            return Result<CommerceRelease>.Fail(
                DeskErrors.NotFound("Attach a saved catalogue edition for this language first."));
        }
        if (!rules.EditionEligible(edition, draft.Snapshot)
            || (edition.PdfSha256 is not null && !rules.PdfEligible(edition, draft.Snapshot)))
        {
            return Invalid<CommerceRelease>(
                "This edition contains inactive or unconfirmed products. "
                + "Prepare a new edition after reviewing its language.");
        }
        if (!edition.ApprovedForPublication)
        {
            var approved = draft.Snapshot with
            {
                CatalogueEditions = draft.Snapshot.CatalogueEditions
                    .Select(e => e.Id == edition.Id ? e with { ApprovedForPublication = true } : e)
                    .ToArray()
            };
            Result<CommerceDraft> written = await WriteDraftAsync(
                connection,
                transaction,
                target with { Snapshot = approved },
                [],
                MaximumBlobBytes,
                token);
            if (written.IsFailure(out AppError? error, out var approvedDraft))
                return Result<CommerceRelease>.Fail(error);
            draft = approvedDraft;
        }
        Result<CommerceRelease> release = await WriteReleaseAsync(
            connection,
            transaction,
            target with { ExpectedRevision = draft.Revision, Snapshot = draft.Snapshot },
            note,
            token);
        if (release.IsSuccess) await transaction.CommitAsync(token);
        return release;
    }

    /// <summary>Appends the next draft revision, storing any new media, inside the caller's transaction.</summary>
    private async Task<Result<CommerceDraft>> WriteDraftAsync(
        DbConnection connection,
        DbTransaction transaction,
        DraftWrite draft,
        IReadOnlyList<CommerceBlob> blobs,
        int maximumBlobBytes,
        CancellationToken token)
    {
        const string profileSql =
            """
            INSERT INTO commerce.profiles(organization_id,brand_id,current_revision,updated_at)
            VALUES(@organizationId,@brandId,@revision,@now)
            ON CONFLICT(organization_id,brand_id) DO UPDATE SET current_revision=@revision,updated_at=@now
            """;
        const string fileSql =
            """
            INSERT INTO commerce.media(brand_id,sha256,content_type,file_id)
            VALUES(@brandId,@sha256,@contentType,@fileId)
            ON CONFLICT(brand_id,sha256) DO UPDATE SET file_id=COALESCE(commerce.media.file_id,EXCLUDED.file_id)
            """;
        const string bytesSql =
            """
            INSERT INTO commerce.media(brand_id,sha256,content_type,bytes)
            VALUES(@brandId,@sha256,@contentType,@bytes) ON CONFLICT DO NOTHING
            """;
        const string revisionSql =
            """
            INSERT INTO commerce.revisions(organization_id,brand_id,revision,snapshot,actor,created_at)
            VALUES(@organizationId,@brandId,@revision,@snapshot::jsonb,@actor,@now)
            """;
        var snapshot = draft.Snapshot!;
        rules.Validate(snapshot);
        var revision = checked(draft.ExpectedRevision + 1);
        var now = time.GetUtcNow();
        var profile = new { organizationId = draft.OrganizationId, brandId = draft.BrandId, revision, now };
        await connection.ExecuteAsync(Command(profileSql, profile, transaction, token));
        foreach (var blob in blobs)
        {
            if (blob.FileId is { } fileId)
            {
                // WoW2 Files holds the bytes, checked before upload; a row stored before Files takes the file in place.
                if (!rules.IsHash(blob.Sha256))
                    return Invalid<CommerceDraft>("Imported media bytes do not match their hash.");
                var file = new
                {
                    brandId = draft.BrandId,
                    sha256 = blob.Sha256,
                    contentType = blob.ContentType,
                    fileId,
                };
                await connection.ExecuteAsync(Command(fileSql, file, transaction, token));
                continue;
            }
            if (blob.Bytes.Length <= 0
                || blob.Bytes.Length > maximumBlobBytes
                || blob.Sha256 != Convert.ToHexStringLower(SHA256.HashData(blob.Bytes)))
                return Invalid<CommerceDraft>("Imported media bytes do not match their hash.");
            var stored = new
            {
                brandId = draft.BrandId,
                sha256 = blob.Sha256,
                contentType = blob.ContentType,
                bytes = blob.Bytes,
            };
            await connection.ExecuteAsync(Command(bytesSql, stored, transaction, token));
        }
        var entry = new
        {
            organizationId = draft.OrganizationId,
            brandId = draft.BrandId,
            revision,
            snapshot = JsonSerializer.Serialize(snapshot, Json),
            actor = draft.Actor,
            now,
        };
        await connection.ExecuteAsync(Command(revisionSql, entry, transaction, token));
        return Result<CommerceDraft>.Ok(new CommerceDraft(draft.BrandId, revision, snapshot, now));
    }

    /// <summary>Publishes the draft revision as the brand's active release, inside the caller's transaction.</summary>
    private async Task<Result<CommerceRelease>> WriteReleaseAsync(
        DbConnection connection,
        DbTransaction transaction,
        DraftWrite draft,
        string note,
        CancellationToken token)
    {
        const string mediaSql = "SELECT 1 FROM commerce.media WHERE brand_id=@brandId AND sha256=@sha256";
        const string duplicateSql =
            """
            SELECT 1 FROM commerce.releases
            WHERE organization_id=@organizationId AND brand_id=@brandId AND source_revision=@sourceRevision
                AND withdrawn_at IS NULL
            """;
        const string versionSql =
            """
            SELECT COALESCE(MAX(revision),0)+1 FROM commerce.releases
            WHERE organization_id=@organizationId AND brand_id=@brandId
            """;
        const string headSql =
            """
            UPDATE commerce.profiles SET public_slug=@slug,active_release_id=@releaseId
            WHERE organization_id=@organizationId AND brand_id=@brandId
            """;
        const string releaseSql =
            """
            INSERT INTO commerce.releases(
                id,organization_id,brand_id,revision,source_revision,snapshot,note,actor,published_at)
            VALUES(
                @releaseId,@organizationId,@brandId,@revision,@sourceRevision,@snapshot::jsonb,@note,@actor,
                @publishedAt)
            """;
        if (string.IsNullOrWhiteSpace(note) || note.Length > 1000)
            return Invalid<CommerceRelease>("Record the reason for publishing (maximum 1000 characters).");
        var snapshot = draft.Snapshot!;
        var organizationId = draft.OrganizationId;
        var brandId = draft.BrandId;
        var sourceRevision = draft.ExpectedRevision;
        rules.Validate(snapshot);
        var approvedHashes = rules.ApprovedHashes(snapshot)
            .Concat(snapshot.CatalogueEditions.Where(e => e.ApprovedForPublication).SelectMany(EditionHashes))
            .Distinct();
        foreach (var hash in approvedHashes)
        {
            int? stored = await connection.ExecuteScalarAsync<int?>(
                Command(mediaSql, new { brandId, sha256 = hash }, transaction, token));
            if (stored is null)
            {
                return Invalid<CommerceRelease>(
                    "Approved image bytes are missing; attach the image before publishing.");
            }
        }
        int? duplicate = await connection.ExecuteScalarAsync<int?>(
            Command(duplicateSql, new { organizationId, brandId, sourceRevision }, transaction, token));
        if (duplicate is not null)
        {
            return Result<CommerceRelease>.Fail(
                DeskErrors.Conflict("This draft revision already has an active release."));
        }
        var revision = await connection.ExecuteScalarAsync<int>(
            Command(versionSql, new { organizationId, brandId }, transaction, token));
        var release = new CommerceRelease(
            Guid.NewGuid(),
            brandId,
            revision,
            sourceRevision,
            time.GetUtcNow(),
            note.Trim(),
            snapshot);
        try
        {
            var head = new { organizationId, brandId, slug = snapshot.Slug, releaseId = release.Id };
            await connection.ExecuteAsync(Command(headSql, head, transaction, token));
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return Result<CommerceRelease>.Fail(DeskErrors.Conflict("That public brand slug is already in use."));
        }
        var entry = new
        {
            releaseId = release.Id,
            organizationId,
            brandId,
            revision,
            sourceRevision,
            snapshot = JsonSerializer.Serialize(snapshot, Json),
            note = release.Note,
            actor = draft.Actor,
            publishedAt = release.PublishedAt,
        };
        await connection.ExecuteAsync(Command(releaseSql, entry, transaction, token));
        return Result<CommerceRelease>.Ok(release);
    }

    /// <summary>Finds the brand in the organization, locking it when asked; an error when it is unavailable.</summary>
    private static async Task<AppError?> BrandMissingAsync(
        DbConnection connection,
        DbTransaction? transaction,
        Guid organizationId,
        Guid brandId,
        bool locked,
        CancellationToken token)
    {
        var sql = "SELECT id FROM preparation.brands WHERE organization_id=@organizationId AND id=@brandId"
            + (locked ? " FOR UPDATE" : "");
        Guid? brand = await connection.ExecuteScalarAsync<Guid?>(
            Command(sql, new { organizationId, brandId }, transaction, token));
        return brand is null ? DeskErrors.NotFound("Brand unavailable.") : null;
    }

    /// <summary>Reads the brand's current draft; none before its first save.</summary>
    private static async Task<CommerceDraft?> ReadAsync(
        DbConnection connection,
        DbTransaction? transaction,
        Guid organizationId,
        Guid brandId,
        CancellationToken token)
    {
        const string sql =
            """
            SELECT p.current_revision,r.snapshot::text,p.updated_at
            FROM commerce.profiles p
            JOIN commerce.revisions r
                ON r.organization_id=p.organization_id AND r.brand_id=p.brand_id AND r.revision=p.current_revision
            WHERE p.organization_id=@organizationId AND p.brand_id=@brandId
            """;
        DraftRow? row = await connection.QueryFirstOrDefaultAsync<DraftRow>(
            Command(sql, new { organizationId, brandId }, transaction, token));
        if (row is null) return null;
        var snapshot = JsonSerializer.Deserialize<CommerceSnapshot>(row.Snapshot, Json)!;
        return new CommerceDraft(brandId, row.CurrentRevision, snapshot, row.UpdatedAt);
    }

    private static IEnumerable<string> EditionHashes(CatalogueEdition edition)
    {
        IEnumerable<string> pdf = edition.PdfSha256 is { } hash ? [hash] : [];
        return pdf.Concat(edition.Pages.SelectMany(page => new[] { page.PngSha256, page.PdfSha256 }));
    }

    private static Result<T> Invalid<T>(string message)
        where T : notnull
    {
        return Result<T>.Fail(DeskErrors.Invalid(message));
    }

    private static CommandDefinition Command(
        string sql,
        object parameters,
        DbTransaction? transaction,
        CancellationToken token)
    {
        return new CommandDefinition(sql, parameters, transaction, cancellationToken: token);
    }

    /// <summary>The brand, actor and expected revision a draft or release write runs against.</summary>
    private sealed record DraftWrite(
        Guid OrganizationId,
        string Actor,
        Guid BrandId,
        int ExpectedRevision,
        CommerceSnapshot? Snapshot);

    /// <summary>A brand's current draft revision, as its columns read.</summary>
    private sealed class DraftRow
    {
        public int CurrentRevision { get; init; }
        public string Snapshot { get; init; } = string.Empty;
        public DateTimeOffset UpdatedAt { get; init; }
    }

    /// <summary>One stored release, as its columns read.</summary>
    private sealed class ReleaseRow
    {
        public Guid Id { get; init; }
        public Guid BrandId { get; init; }
        public int Revision { get; init; }
        public int SourceRevision { get; init; }
        public DateTimeOffset PublishedAt { get; init; }
        public string Note { get; init; } = string.Empty;
        public string Snapshot { get; init; } = string.Empty;
        public DateTimeOffset? WithdrawnAt { get; init; }

        public CommerceRelease ToRelease()
        {
            return new CommerceRelease(
                Id,
                BrandId,
                Revision,
                SourceRevision,
                PublishedAt,
                Note,
                JsonSerializer.Deserialize<CommerceSnapshot>(Snapshot, Json)!,
                WithdrawnAt);
        }
    }

    /// <summary>One brand media row: its bytes, or the WoW2 Files file holding them.</summary>
    private sealed class BlobRow
    {
        public string ContentType { get; init; } = string.Empty;
        public byte[]? Bytes { get; init; }
        public string? FileId { get; init; }
    }

    /// <summary>One stored creative import.</summary>
    private sealed class CreativeImportRow
    {
        public Guid Id { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
        public string Snapshot { get; init; } = string.Empty;
    }
}
