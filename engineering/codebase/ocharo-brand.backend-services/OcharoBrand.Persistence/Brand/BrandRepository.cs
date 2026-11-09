using Microsoft.EntityFrameworkCore;
using Npgsql;
using OcharoBrand.Application.Brand.Constants;
using OcharoBrand.Application.Brand.Errors;
using OcharoBrand.Application.Brand.Repositories;
using OcharoBrand.Domain.Brand;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace OcharoBrand.Persistence.Brand;

/// <summary>Brand state in schema <c>brand</c> of the product database; draft saves check the revision inside one
/// transaction, and a save that loses a race to another replica or request answers the same conflict.</summary>
public sealed class BrandRepository(OcharoBrandDbContext db) : IBrandRepository
{
    public Task<DraftRecord?> FindDraftAsync(CancellationToken ct) => db.Drafts.AsNoTracking().FirstOrDefaultAsync(d => d.Id == BrandDraftConstants.DraftId, ct);

    public async Task<Result<DraftRecord>> SaveDraftAsync(DraftRecord record, int expectedRevision, string summary, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var current = await db.Drafts.FirstOrDefaultAsync(d => d.Id == record.Id, ct);
        if ((current?.Revision ?? 0) != expectedRevision)
            return Result<DraftRecord>.Fail(BrandErrors.Conflict($"The brand was saved elsewhere (now revision {current?.Revision ?? 0}). Reload before saving."));
        if (current is null) db.Drafts.Add(record);
        else
        {
            current.Revision = record.Revision;
            current.UpdatedAt = record.UpdatedAt;
            current.Json = record.Json;
        }

        db.DraftRevisions.Add(new DraftRevisionRecord { Id = Guid.NewGuid(), Revision = record.Revision, SavedAt = record.UpdatedAt, Summary = summary, Json = record.Json });
        try
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException failure) when (IsLostRace(failure))
        {
            // Another save took this revision between the read and the write: its row lock decided the order.
            db.ChangeTracker.Clear();
            return Result<DraftRecord>.Fail(BrandErrors.Conflict("The brand was saved elsewhere. Reload before saving."));
        }

        db.ChangeTracker.Clear();
        return Result<DraftRecord>.Ok(record);
    }

    public async Task<IReadOnlyList<DraftRevisionRecord>> GetDraftRevisionsAsync(CancellationToken ct) => await db.DraftRevisions.AsNoTracking().ToListAsync(ct);

    public async Task<IReadOnlyList<LogoVersionRecord>> GetLogoVersionsAsync(CancellationToken ct) => await db.LogoVersions.AsNoTracking().ToListAsync(ct);

    public Task<LogoVersionRecord?> FindLogoVersionAsync(string id, CancellationToken ct) => db.LogoVersions.AsNoTracking().FirstOrDefaultAsync(v => v.Id == id, ct);

    public async Task SaveLogoVersionAsync(LogoVersionRecord record, CancellationToken ct)
    {
        var current = await db.LogoVersions.FirstOrDefaultAsync(v => v.Id == record.Id, ct);
        if (current is null) db.LogoVersions.Add(record);
        else current.Json = record.Json;
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
    }

    public async Task<IReadOnlyList<ReleaseRecord>> GetReleasesAsync(CancellationToken ct) => await db.Releases.AsNoTracking().ToListAsync(ct);

    public Task<ReleaseRecord?> FindLatestReleaseAsync(CancellationToken ct) => db.Releases.AsNoTracking().OrderByDescending(r => r.Version).FirstOrDefaultAsync(ct);

    public Task<ReleaseRecord?> FindReleaseAsync(int version, CancellationToken ct) => db.Releases.AsNoTracking().FirstOrDefaultAsync(r => r.Version == version, ct);

    public async Task<Result<ReleaseRecord>> InsertReleaseAsync(ReleaseRecord record, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (await db.Releases.AnyAsync(r => r.Version == record.Version, ct))
            return Result<ReleaseRecord>.Fail(BrandErrors.Conflict("Another release was just published. Reload."));
        db.Releases.Add(record);
        try
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException failure) when (IsLostRace(failure))
        {
            db.ChangeTracker.Clear();
            return Result<ReleaseRecord>.Fail(BrandErrors.Conflict("Another release was just published. Reload."));
        }

        db.ChangeTracker.Clear();
        return Result<ReleaseRecord>.Ok(record);
    }

    public async Task<IReadOnlyList<AppearanceRecord>> GetAppearancesAsync(CancellationToken ct) => await db.Appearances.AsNoTracking().ToListAsync(ct);

    public Task<AppearanceRecord?> FindAppearanceAsync(string id, CancellationToken ct) => db.Appearances.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task SaveAppearanceAsync(AppearanceRecord record, CancellationToken ct)
    {
        var current = await db.Appearances.FirstOrDefaultAsync(a => a.Id == record.Id, ct);
        if (current is null) db.Appearances.Add(record);
        else
        {
            current.StartsAt = record.StartsAt;
            current.EndsAt = record.EndsAt;
            current.Status = record.Status;
            current.Json = record.Json;
        }

        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
    }

    public Task<MediaBlobRecord?> FindMediaAsync(string sha256, CancellationToken ct) => db.MediaBlobs.AsNoTracking().FirstOrDefaultAsync(m => m.Sha256 == sha256, ct);

    /// <summary>Records a stored file once per SHA-256; a file another request recorded first stays as it is.</summary>
    public async Task SaveMediaAsync(MediaBlobRecord record, CancellationToken ct)
    {
        var storedAt = record.StoredAt.ToUniversalTime();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO brand.media_blobs (sha256, content_type, byte_length, file_id, blob_path, preview_path, width, height, stored_at)
            VALUES ({record.Sha256}, {record.ContentType}, {record.ByteLength}, {record.FileId}, {record.BlobPath}, {record.PreviewPath},
                {record.Width}, {record.Height}, {storedAt})
            ON CONFLICT (sha256) DO NOTHING
            """, ct);
    }

    public async Task<IReadOnlyList<MediaBlobRecord>> GetMediaWithoutFileAsync(CancellationToken ct) =>
        await db.MediaBlobs.AsNoTracking().Where(m => m.FileId == null).ToListAsync(ct);

    public async Task SetMediaFileAsync(string sha256, string fileId, CancellationToken ct)
    {
        await db.MediaBlobs.Where(m => m.Sha256 == sha256).ExecuteUpdateAsync(set => set.SetProperty(m => m.FileId, fileId), ct);
        db.ChangeTracker.Clear();
    }

    public async Task<IReadOnlyList<ConsumerRecord>> GetConsumersAsync(CancellationToken ct) => await db.Consumers.AsNoTracking().ToListAsync(ct);

    /// <summary>Records an app's latest read in one statement, so two first reads of one app never collide.</summary>
    public async Task TouchConsumerAsync(ConsumerRecord record, CancellationToken ct)
    {
        var seenAt = record.LastSeenAt.ToUniversalTime();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO brand.consumers (app, last_seen_at, last_release, last_path)
            VALUES ({record.App}, {seenAt}, {record.LastRelease}, {record.LastPath})
            ON CONFLICT (app) DO UPDATE SET last_seen_at = EXCLUDED.last_seen_at, last_release = EXCLUDED.last_release,
                last_path = EXCLUDED.last_path
            """, ct);
    }

    /// <summary>Tells whether a write lost to a concurrent one: a stale revision, or a key another request took first.</summary>
    private static bool IsLostRace(DbUpdateException failure) =>
        failure is DbUpdateConcurrencyException
        || failure.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
