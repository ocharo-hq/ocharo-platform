using Microsoft.EntityFrameworkCore;
using OcharoCatalogue.Application.Catalogue.Core.Errors;
using OcharoCatalogue.Application.Catalogue.Core.Repositories;
using OcharoCatalogue.Application.Catalogue.Core.Serializers;
using OcharoCatalogue.Application.Catalogue.Publishing.Hashers;
using OcharoCatalogue.Domain.Catalogue.Entities;

namespace OcharoCatalogue.Persistence.Catalogue;

/// <summary>Stores catalogue designs, releases and publications; revision and source checks run in the same SQLite
/// transaction as every write, and a failed check fails as a conflict.</summary>
public sealed class CatalogueDesignRepository(OcharoCatalogueDbContext db) : IDesignRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<CatalogueDesignRecord>> ListAsync(bool archived, CancellationToken ct)
    {
        return await db.CatalogueDesigns.AsNoTracking()
            .Where(design => archived || !design.Archived)
            .OrderByDescending(design => design.UpdatedAtUtc)
            .ToArrayAsync(ct);
    }

    /// <inheritdoc />
    public Task<CatalogueDesignRecord?> GetAsync(Guid id, CancellationToken ct)
    {
        return db.CatalogueDesigns.AsNoTracking().FirstOrDefaultAsync(design => design.Id == id, ct);
    }

    /// <inheritdoc />
    public async Task<CatalogueDesignRecord> SaveAsync(
        CatalogueDesignRecord record,
        int? expectedRevision,
        CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var current = await db.CatalogueDesigns.FirstOrDefaultAsync(design => design.Id == record.Id, ct);
        var slugTaken = await db.CatalogueDesigns.AnyAsync(
            design => design.Id != record.Id
                && !design.Archived
                && design.Slug == record.Slug
                && design.Locale == record.Locale,
            ct);
        if (slugTaken)
        {
            throw Conflict("This catalogue slug is already used in this language.");
        }

        if (expectedRevision is null)
        {
            if (current is not null)
            {
                throw Conflict("The design already exists.");
            }

            record.Revision = 1;
            db.CatalogueDesigns.Add(record);
        }
        else
        {
            Check(current, expectedRevision.Value);
            record.CreatedAtUtc = current!.CreatedAtUtc;
            record.Revision = current.Revision + 1;
            db.Entry(current).CurrentValues.SetValues(record);
        }

        db.CatalogueDesignRevisions.Add(new CatalogueDesignRevisionRecord
        {
            Id = Guid.NewGuid(),
            DesignId = record.Id,
            Revision = record.Revision,
            Title = record.Title,
            SavedAtUtc = record.UpdatedAtUtc,
            Json = record.Json,
        });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        db.ChangeTracker.Clear();
        return record;
    }

    /// <inheritdoc />
    public async Task ArchiveAsync(
        Guid id,
        int expectedRevision,
        DateTimeOffset now,
        CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var current = await db.CatalogueDesigns.FirstOrDefaultAsync(design => design.Id == id, ct);
        Check(current, expectedRevision);
        current!.Archived = true;
        current.Revision++;
        current.UpdatedAtUtc = now;
        db.CatalogueDesignRevisions.Add(new CatalogueDesignRevisionRecord
        {
            Id = Guid.NewGuid(),
            DesignId = id,
            Revision = current.Revision,
            Title = current.Title,
            SavedAtUtc = now,
            Json = current.Json,
        });
        await db.CataloguePublications.Where(publication => publication.DesignId == id).ExecuteDeleteAsync(ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        db.ChangeTracker.Clear();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CatalogueDesignRevisionRecord>> RevisionsAsync(Guid id, CancellationToken ct)
    {
        return await db.CatalogueDesignRevisions.AsNoTracking()
            .Where(revision => revision.DesignId == id)
            .OrderByDescending(revision => revision.Revision)
            .ToArrayAsync(ct);
    }

    /// <inheritdoc />
    public Task<CatalogueDesignRevisionRecord?> RevisionAsync(
        Guid id,
        int revision,
        CancellationToken ct)
    {
        return db.CatalogueDesignRevisions.AsNoTracking()
            .FirstOrDefaultAsync(saved => saved.DesignId == id && saved.Revision == revision, ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CatalogueReleaseRecord>> ReleasesAsync(Guid designId, CancellationToken ct)
    {
        return await db.CatalogueReleases.AsNoTracking()
            .Where(release => release.DesignId == designId)
            .OrderByDescending(release => release.CreatedAtUtc)
            .ToArrayAsync(ct);
    }

    /// <inheritdoc />
    public Task<CatalogueReleaseRecord?> ReleaseAsync(Guid releaseId, CancellationToken ct)
    {
        return db.CatalogueReleases.AsNoTracking().FirstOrDefaultAsync(release => release.Id == releaseId, ct);
    }

    /// <inheritdoc />
    public async Task PrepareAsync(
        CatalogueReleaseRecord release,
        IReadOnlyList<CatalogueReleaseAssetRecord> assets,
        IReadOnlyList<CatalogueReleaseFileRecord> files,
        CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var design = await db.CatalogueDesigns.AsNoTracking().FirstOrDefaultAsync(
            row => row.Id == release.DesignId,
            ct);
        Check(design, release.DesignRevision);
        await CheckFingerprintAsync(release, ct);
        foreach (var asset in assets.DistinctBy(asset => asset.Sha256))
        {
            if (!await db.CatalogueReleaseAssets.AnyAsync(row => row.Sha256 == asset.Sha256, ct))
            {
                db.CatalogueReleaseAssets.Add(asset);
            }
        }

        foreach (var file in files.DistinctBy(file => file.Sha256))
        {
            if (!await db.CatalogueReleaseFiles.AnyAsync(row => row.Sha256 == file.Sha256, ct))
            {
                db.CatalogueReleaseFiles.Add(file);
            }
        }

        db.CatalogueReleases.Add(release);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        db.ChangeTracker.Clear();
    }

    /// <inheritdoc />
    public async Task<CatalogueReleaseRecord> PublishAsync(
        Guid designId,
        Guid releaseId,
        int expectedRevision,
        DateTimeOffset now,
        CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var design = await db.CatalogueDesigns.AsNoTracking().FirstOrDefaultAsync(row => row.Id == designId, ct);
        Check(design, expectedRevision);
        var release = await db.CatalogueReleases.FirstOrDefaultAsync(
            row => row.Id == releaseId && row.DesignId == designId,
            ct)
            ?? throw CatalogueErrors.NotFound("Prepared release").ToException();
        if (release.WithdrawnAtUtc is not null)
        {
            throw Conflict("This release was withdrawn. Prepare a new release before publishing.");
        }

        if (release.DesignRevision != expectedRevision)
        {
            throw Conflict("The saved design changed. Prepare it again before publishing.");
        }

        await CheckFingerprintAsync(release, ct);
        var key = release.Slug + ":" + release.Locale;
        var current = await db.CataloguePublications.FirstOrDefaultAsync(publication => publication.Key == key, ct);
        if (current is not null && current.DesignId != designId)
        {
            throw Conflict("Another catalogue publishes this slug and language.");
        }

        await db.CataloguePublications
            .Where(publication => publication.DesignId == designId && publication.Key != key)
            .ExecuteDeleteAsync(ct);
        if (current is null)
        {
            db.CataloguePublications.Add(
                new CataloguePublicationRecord { Key = key, DesignId = designId, ReleaseId = releaseId });
        }
        else
        {
            current.ReleaseId = releaseId;
        }

        release.PublishedAtUtc ??= now;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        db.ChangeTracker.Clear();
        return release;
    }

    /// <inheritdoc />
    public async Task WithdrawAsync(
        Guid designId,
        int expectedRevision,
        DateTimeOffset now,
        CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var design = await db.CatalogueDesigns.AsNoTracking().FirstOrDefaultAsync(row => row.Id == designId, ct);
        Check(design, expectedRevision);
        await db.CatalogueReleases
            .Where(
                release => release.DesignId == designId
                    && release.PublishedAtUtc != null
                    && release.WithdrawnAtUtc == null)
            .ExecuteUpdateAsync(set => set.SetProperty(release => release.WithdrawnAtUtc, now), ct);
        await db.CataloguePublications.Where(publication => publication.DesignId == designId).ExecuteDeleteAsync(ct);
        await tx.CommitAsync(ct);
        db.ChangeTracker.Clear();
    }

    /// <inheritdoc />
    public async Task<CatalogueReleaseRecord?> PublishedAsync(
        string slug,
        string locale,
        Guid? releaseId,
        CancellationToken ct)
    {
        if (releaseId is not null)
        {
            return await db.CatalogueReleases.AsNoTracking()
                .Where(release => release.Id == releaseId
                    && release.Slug == slug
                    && release.Locale == locale
                    && release.PublishedAtUtc != null
                    && release.WithdrawnAtUtc == null
                    && db.CatalogueDesigns.Any(design => design.Id == release.DesignId && !design.Archived))
                .FirstOrDefaultAsync(ct);
        }

        var current = await db.CataloguePublications.AsNoTracking()
            .FirstOrDefaultAsync(publication => publication.Key == slug + ":" + locale, ct);
        if (current is null)
        {
            return null;
        }

        return await db.CatalogueReleases.AsNoTracking()
            .FirstOrDefaultAsync(
                release => release.Id == current.ReleaseId
                    && release.WithdrawnAtUtc == null
                    && db.CatalogueDesigns.Any(design => design.Id == release.DesignId && !design.Archived),
                ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CatalogueReleaseRecord>> PublicationsAsync(CancellationToken ct)
    {
        return await db.CatalogueReleases.AsNoTracking()
            .Where(release => release.WithdrawnAtUtc == null
                && db.CatalogueDesigns.Any(design => design.Id == release.DesignId && !design.Archived)
                && db.CataloguePublications.Any(publication => publication.ReleaseId == release.Id))
            .ToArrayAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CatalogueReleaseRecord>> PublicReleasesAsync(CancellationToken ct)
    {
        return await db.CatalogueReleases.AsNoTracking()
            .Where(release => release.PublishedAtUtc != null
                && release.WithdrawnAtUtc == null
                && db.CatalogueDesigns.Any(design => design.Id == release.DesignId && !design.Archived))
            .ToArrayAsync(ct);
    }

    /// <inheritdoc />
    public Task<CatalogueReleaseAssetRecord?> AssetAsync(string sha256, CancellationToken ct)
    {
        return db.CatalogueReleaseAssets.AsNoTracking().FirstOrDefaultAsync(asset => asset.Sha256 == sha256, ct);
    }

    /// <inheritdoc />
    public Task<CatalogueReleaseFileRecord?> ReleaseFileAsync(string sha256, CancellationToken ct)
    {
        return db.CatalogueReleaseFiles.AsNoTracking().FirstOrDefaultAsync(file => file.Sha256 == sha256, ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, string>> AssetsWithoutFileAsync(CancellationToken ct)
    {
        return await db.CatalogueReleaseAssets.AsNoTracking()
            .Where(asset => !db.CatalogueReleaseFiles.Any(file => file.Sha256 == asset.Sha256))
            .Select(asset => new { asset.Sha256, asset.ContentType })
            .ToDictionaryAsync(
                asset => asset.Sha256,
                asset => asset.ContentType,
                StringComparer.Ordinal,
                ct);
    }

    /// <inheritdoc />
    public async Task SaveReleaseFileAsync(CatalogueReleaseFileRecord file, CancellationToken ct)
    {
        if (await db.CatalogueReleaseFiles.AnyAsync(row => row.Sha256 == file.Sha256, ct))
        {
            return;
        }

        db.CatalogueReleaseFiles.Add(file);
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
    }

    private static void Check(CatalogueDesignRecord? current, int revision)
    {
        if (current is null)
        {
            throw CatalogueErrors.NotFound("Catalogue design").ToException();
        }

        if (current.Archived)
        {
            throw Conflict("This catalogue is archived.");
        }

        if (revision < 1 || revision == int.MaxValue || current.Revision != revision)
        {
            throw Conflict("The catalogue changed. Reload before saving.");
        }
    }

    private async Task CheckFingerprintAsync(CatalogueReleaseRecord release, CancellationToken ct)
    {
        var products = await db.Products.AsNoTracking().ToArrayAsync(ct);
        var settings = await db.Settings.AsNoTracking().FirstOrDefaultAsync(ct);
        var fingerprint = DesignFingerprint.Create(
            products,
            settings,
            CatalogueJson.Read<string[]>(release.ProductIdsJson));
        if (fingerprint != release.Fingerprint)
        {
            throw Conflict("Product information changed. Review and prepare the catalogue again.");
        }
    }

    private static Exception Conflict(string message)
    {
        return CatalogueErrors.Conflict(message).ToException();
    }
}
