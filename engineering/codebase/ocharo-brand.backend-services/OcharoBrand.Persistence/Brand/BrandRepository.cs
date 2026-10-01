using Microsoft.EntityFrameworkCore;
using OcharoBrand.Application.Brand;
using OcharoBrand.Domain.Brand;

namespace OcharoBrand.Persistence.Brand;

/// <summary>SQLite-backed brand state; draft saves check the revision inside one transaction.</summary>
public sealed class BrandRepository(OcharoBrandDbContext db) : IBrandRepository
{
    public Task<DraftRecord?> FindDraftAsync(CancellationToken ct) => db.Drafts.AsNoTracking().FirstOrDefaultAsync(d => d.Id == BrandService.DraftId, ct);

    public async Task<DraftRecord> SaveDraftAsync(DraftRecord record, int expectedRevision, string summary, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var current = await db.Drafts.FirstOrDefaultAsync(d => d.Id == record.Id, ct);
        if ((current?.Revision ?? 0) != expectedRevision)
            throw BrandException.Conflict($"The brand was saved elsewhere (now revision {current?.Revision ?? 0}). Reload before saving.");
        if (current is null) db.Drafts.Add(record);
        else
        {
            current.Revision = record.Revision;
            current.UpdatedAt = record.UpdatedAt;
            current.Json = record.Json;
        }

        db.DraftRevisions.Add(new DraftRevisionRecord { Id = Guid.NewGuid(), Revision = record.Revision, SavedAt = record.UpdatedAt, Summary = summary, Json = record.Json });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        db.ChangeTracker.Clear();
        return record;
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

    public async Task<ReleaseRecord> InsertReleaseAsync(ReleaseRecord record, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (await db.Releases.AnyAsync(r => r.Version == record.Version, ct)) throw BrandException.Conflict("Another release was just published. Reload.");
        db.Releases.Add(record);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        db.ChangeTracker.Clear();
        return record;
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

    public async Task SaveMediaAsync(MediaBlobRecord record, CancellationToken ct)
    {
        if (await db.MediaBlobs.AnyAsync(m => m.Sha256 == record.Sha256, ct)) return;
        db.MediaBlobs.Add(record);
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
    }

    public async Task<IReadOnlyList<ConsumerRecord>> GetConsumersAsync(CancellationToken ct) => await db.Consumers.AsNoTracking().ToListAsync(ct);

    public async Task TouchConsumerAsync(ConsumerRecord record, CancellationToken ct)
    {
        var current = await db.Consumers.FirstOrDefaultAsync(c => c.App == record.App, ct);
        if (current is null) db.Consumers.Add(record);
        else
        {
            current.LastSeenAt = record.LastSeenAt;
            current.LastRelease = record.LastRelease;
            current.LastPath = record.LastPath;
        }

        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
    }
}
