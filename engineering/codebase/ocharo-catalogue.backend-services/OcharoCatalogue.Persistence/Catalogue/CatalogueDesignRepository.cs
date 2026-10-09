using Microsoft.EntityFrameworkCore;
using OcharoCatalogue.Application.Catalogue;
using OcharoCatalogue.Application.Catalogue.Repositories;
using OcharoCatalogue.Application.Catalogue.Services;
using OcharoCatalogue.Domain.Catalogue.Entities;
namespace OcharoCatalogue.Persistence.Catalogue;

/// <summary>Revision and source checks execute in the same SQLite transaction as every write.</summary>
public sealed class CatalogueDesignRepository(OcharoCatalogueDbContext db):IDesignRepository
{
    public async Task<IReadOnlyList<CatalogueDesignRecord>> ListAsync(bool archived,CancellationToken ct)=>await db.CatalogueDesigns.AsNoTracking().Where(x=>archived||!x.Archived).OrderByDescending(x=>x.UpdatedAtUtc).ToArrayAsync(ct);
    public Task<CatalogueDesignRecord?> GetAsync(Guid id,CancellationToken ct)=>db.CatalogueDesigns.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==id,ct);
    public async Task<CatalogueDesignRecord> SaveAsync(CatalogueDesignRecord record,int? expectedRevision,CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        var current=await db.CatalogueDesigns.FirstOrDefaultAsync(x=>x.Id==record.Id,ct);
        if(await db.CatalogueDesigns.AnyAsync(x=>x.Id!=record.Id&&!x.Archived&&x.Slug==record.Slug&&x.Locale==record.Locale,ct))throw CatalogueException.Conflict("This catalogue slug is already used in this language.");
        if(expectedRevision is null){if(current is not null)throw CatalogueException.Conflict("The design already exists.");record.Revision=1;db.CatalogueDesigns.Add(record);}
        else{Check(current,expectedRevision.Value);record.CreatedAtUtc=current!.CreatedAtUtc;record.Revision=current.Revision+1;db.Entry(current).CurrentValues.SetValues(record);}
        db.CatalogueDesignRevisions.Add(new(){Id=Guid.NewGuid(),DesignId=record.Id,Revision=record.Revision,Title=record.Title,SavedAtUtc=record.UpdatedAtUtc,Json=record.Json});
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);db.ChangeTracker.Clear();return record;
    }
    public async Task ArchiveAsync(Guid id,int expectedRevision,DateTimeOffset now,CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(ct);var current=await db.CatalogueDesigns.FirstOrDefaultAsync(x=>x.Id==id,ct);Check(current,expectedRevision);
        current!.Archived=true;current.Revision++;current.UpdatedAtUtc=now;
        db.CatalogueDesignRevisions.Add(new(){Id=Guid.NewGuid(),DesignId=id,Revision=current.Revision,Title=current.Title,SavedAtUtc=now,Json=current.Json});
        await db.CataloguePublications.Where(x=>x.DesignId==id).ExecuteDeleteAsync(ct);
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);db.ChangeTracker.Clear();
    }
    public async Task<IReadOnlyList<CatalogueDesignRevisionRecord>> RevisionsAsync(Guid id,CancellationToken ct)=>await db.CatalogueDesignRevisions.AsNoTracking().Where(x=>x.DesignId==id).OrderByDescending(x=>x.Revision).ToArrayAsync(ct);
    public Task<CatalogueDesignRevisionRecord?> RevisionAsync(Guid id,int revision,CancellationToken ct)=>db.CatalogueDesignRevisions.AsNoTracking().FirstOrDefaultAsync(x=>x.DesignId==id&&x.Revision==revision,ct);
    public async Task<IReadOnlyList<CatalogueReleaseRecord>> ReleasesAsync(Guid id,CancellationToken ct)=>await db.CatalogueReleases.AsNoTracking().Where(x=>x.DesignId==id).OrderByDescending(x=>x.CreatedAtUtc).ToArrayAsync(ct);
    public Task<CatalogueReleaseRecord?> ReleaseAsync(Guid id,CancellationToken ct)=>db.CatalogueReleases.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==id,ct);
    public async Task PrepareAsync(CatalogueReleaseRecord release,IReadOnlyList<CatalogueReleaseAssetRecord> assets,CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(ct);Check(await db.CatalogueDesigns.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==release.DesignId,ct),release.DesignRevision);
        await CheckFingerprint(release,ct);
        foreach(var asset in assets.DistinctBy(a=>a.Sha256))if(!await db.CatalogueReleaseAssets.AnyAsync(x=>x.Sha256==asset.Sha256,ct))db.CatalogueReleaseAssets.Add(asset);
        db.CatalogueReleases.Add(release);await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);db.ChangeTracker.Clear();
    }
    public async Task<CatalogueReleaseRecord> PublishAsync(Guid designId,Guid releaseId,int expectedRevision,DateTimeOffset now,CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(ct);Check(await db.CatalogueDesigns.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==designId,ct),expectedRevision);
        var release=await db.CatalogueReleases.FirstOrDefaultAsync(x=>x.Id==releaseId&&x.DesignId==designId,ct)??throw CatalogueException.NotFound("Prepared release");
        if(release.WithdrawnAtUtc is not null)throw CatalogueException.Conflict("This release was withdrawn. Prepare a new release before publishing.");
        if(release.DesignRevision!=expectedRevision)throw CatalogueException.Conflict("The saved design changed. Prepare it again before publishing.");
        await CheckFingerprint(release,ct);
        var key=release.Slug+":"+release.Locale;
        var current=await db.CataloguePublications.FirstOrDefaultAsync(x=>x.Key==key,ct);
        if(current is not null&&current.DesignId!=designId)throw CatalogueException.Conflict("Another catalogue publishes this slug and language.");
        await db.CataloguePublications.Where(x=>x.DesignId==designId&&x.Key!=key).ExecuteDeleteAsync(ct);
        if(current is null)db.CataloguePublications.Add(new(){Key=key,DesignId=designId,ReleaseId=releaseId});else current.ReleaseId=releaseId;
        release.PublishedAtUtc??=now;await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);db.ChangeTracker.Clear();return release;
    }
    public async Task WithdrawAsync(Guid designId,int expectedRevision,DateTimeOffset now,CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        Check(await db.CatalogueDesigns.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==designId,ct),expectedRevision);
        await db.CatalogueReleases.Where(x=>x.DesignId==designId&&x.PublishedAtUtc!=null&&x.WithdrawnAtUtc==null).ExecuteUpdateAsync(set=>set.SetProperty(x=>x.WithdrawnAtUtc,now),ct);
        await db.CataloguePublications.Where(x=>x.DesignId==designId).ExecuteDeleteAsync(ct);
        await tx.CommitAsync(ct);db.ChangeTracker.Clear();
    }
    public async Task<CatalogueReleaseRecord?> PublishedAsync(string slug,string locale,Guid? releaseId,CancellationToken ct)
    {
        if(releaseId is not null)return await db.CatalogueReleases.AsNoTracking().Where(x=>x.Id==releaseId&&x.Slug==slug&&x.Locale==locale&&x.PublishedAtUtc!=null&&x.WithdrawnAtUtc==null&&db.CatalogueDesigns.Any(d=>d.Id==x.DesignId&&!d.Archived)).FirstOrDefaultAsync(ct);
        var current=await db.CataloguePublications.AsNoTracking().FirstOrDefaultAsync(x=>x.Key==slug+":"+locale,ct);return current is null?null:await db.CatalogueReleases.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==current.ReleaseId&&x.WithdrawnAtUtc==null&&db.CatalogueDesigns.Any(d=>d.Id==x.DesignId&&!d.Archived),ct);
    }
    public async Task<IReadOnlyList<CatalogueReleaseRecord>> PublicationsAsync(CancellationToken ct)=>await db.CatalogueReleases.AsNoTracking().Where(x=>x.WithdrawnAtUtc==null&&db.CatalogueDesigns.Any(d=>d.Id==x.DesignId&&!d.Archived)&&db.CataloguePublications.Any(p=>p.ReleaseId==x.Id)).ToArrayAsync(ct);
    public async Task<IReadOnlyList<CatalogueReleaseRecord>> PublicReleasesAsync(CancellationToken ct)
    {
        return await db.CatalogueReleases.AsNoTracking()
            .Where(release => release.PublishedAtUtc != null && release.WithdrawnAtUtc == null
                && db.CatalogueDesigns.Any(design => design.Id == release.DesignId && !design.Archived))
            .ToArrayAsync(ct);
    }
    public Task<CatalogueReleaseAssetRecord?> AssetAsync(string hash,CancellationToken ct)=>db.CatalogueReleaseAssets.AsNoTracking().FirstOrDefaultAsync(x=>x.Sha256==hash,ct);
    private static void Check(CatalogueDesignRecord? current,int revision){if(current is null)throw CatalogueException.NotFound("Catalogue design");if(current.Archived)throw CatalogueException.Conflict("This catalogue is archived.");if(revision<1||revision==int.MaxValue||current.Revision!=revision)throw CatalogueException.Conflict("The catalogue changed. Reload before saving.");}
    private async Task CheckFingerprint(CatalogueReleaseRecord release,CancellationToken ct)
    {
        var products=await db.Products.AsNoTracking().ToArrayAsync(ct);var settings=await db.Settings.AsNoTracking().FirstOrDefaultAsync(ct);
        if(DesignFingerprint.Create(products,settings,CatalogueJson.Read<string[]>(release.ProductIdsJson))!=release.Fingerprint)throw CatalogueException.Conflict("Product information changed. Review and prepare the catalogue again.");
    }
}
