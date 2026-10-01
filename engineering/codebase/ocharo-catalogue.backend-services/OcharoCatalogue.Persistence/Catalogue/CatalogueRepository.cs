using Microsoft.EntityFrameworkCore;
using OcharoCatalogue.Application.Catalogue;
using OcharoCatalogue.Application.Catalogue.Repositories;
using OcharoCatalogue.Domain.Catalogue.Entities;

namespace OcharoCatalogue.Persistence.Catalogue;

/// <summary>SQLite-backed catalogue state with revision checks enforced inside one transaction.</summary>
public sealed class CatalogueRepository(OcharoCatalogueDbContext db) : ICatalogueRepository
{
    public async Task<IReadOnlyList<ProductRecord>> GetProductsAsync(CancellationToken ct) =>
        await db.Products.AsNoTracking().ToListAsync(ct);

    public Task<ProductRecord?> FindProductAsync(string id, CancellationToken ct) =>
        db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<ProductRecord> InsertProductAsync(ProductRecord record, string summary, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (await db.Products.AnyAsync(p => p.Id == record.Id, ct))
            throw CatalogueException.Conflict($"A product with id '{record.Id}' already exists.");
        db.Products.Add(record);
        db.ProductRevisions.Add(Snapshot(record, summary));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        db.ChangeTracker.Clear();
        return record;
    }

    public async Task<ProductRecord> UpdateProductAsync(ProductRecord record, int expectedRevision, string summary, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var current = await db.Products.FirstOrDefaultAsync(p => p.Id == record.Id, ct)
            ?? throw CatalogueException.NotFound("Product");
        if (current.Revision != expectedRevision)
            throw CatalogueException.Conflict($"The product was saved elsewhere (now revision {current.Revision}). Reload before saving.");
        current.GarmentType = record.GarmentType;
        current.Status = record.Status;
        current.SortOrder = record.SortOrder;
        current.Revision = expectedRevision + 1;
        current.UpdatedAt = record.UpdatedAt;
        current.Json = record.Json;
        db.ProductRevisions.Add(Snapshot(current, summary));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        db.ChangeTracker.Clear();
        return current;
    }

    public async Task<IReadOnlyList<ProductRevisionRecord>> GetRevisionsAsync(string productId, CancellationToken ct) =>
        await db.ProductRevisions.AsNoTracking().Where(r => r.ProductId == productId).ToListAsync(ct);

    public Task<ProductRevisionRecord?> FindRevisionAsync(string productId, int revision, CancellationToken ct) =>
        db.ProductRevisions.AsNoTracking().FirstOrDefaultAsync(r => r.ProductId == productId && r.Revision == revision, ct);

    public async Task<IReadOnlyList<CollectionRecord>> GetCollectionsAsync(CancellationToken ct) =>
        await db.Collections.AsNoTracking().ToListAsync(ct);

    public Task<CollectionRecord?> FindCollectionAsync(string id, CancellationToken ct) =>
        db.Collections.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<CollectionRecord> SaveCollectionAsync(CollectionRecord record, int? expectedRevision, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var current = await db.Collections.FirstOrDefaultAsync(c => c.Id == record.Id, ct);
        if (expectedRevision is null)
        {
            if (current is not null) throw CatalogueException.Conflict($"Collection '{record.Id}' already exists.");
            record.Revision = 1;
            db.Collections.Add(record);
        }
        else
        {
            if (current is null) throw CatalogueException.NotFound("Collection");
            if (current.Revision != expectedRevision)
                throw CatalogueException.Conflict("The collection was saved elsewhere. Reload before saving.");
            current.SortOrder = record.SortOrder;
            current.Revision = expectedRevision.Value + 1;
            current.UpdatedAt = record.UpdatedAt;
            current.Json = record.Json;
            record = current;
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        db.ChangeTracker.Clear();
        return record;
    }

    public async Task DeleteCollectionAsync(string id, CancellationToken ct) =>
        await db.Collections.Where(c => c.Id == id).ExecuteDeleteAsync(ct);

    public Task<MediaBlobRecord?> FindMediaAsync(string sha256, CancellationToken ct) =>
        db.MediaBlobs.AsNoTracking().FirstOrDefaultAsync(m => m.Sha256 == sha256, ct);

    public async Task SaveMediaAsync(MediaBlobRecord record, CancellationToken ct)
    {
        if (await db.MediaBlobs.AnyAsync(m => m.Sha256 == record.Sha256, ct)) return;
        db.MediaBlobs.Add(record);
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
    }

    public Task<SettingsRecord?> FindSettingsAsync(CancellationToken ct) =>
        db.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Id == Application.Catalogue.Services.SettingsService.SettingsId, ct);

    public async Task<SettingsRecord> SaveSettingsAsync(SettingsRecord record, int? expectedRevision, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var current = await db.Settings.FirstOrDefaultAsync(s => s.Id == record.Id, ct);
        if (current is null)
        {
            if (expectedRevision is not null) throw CatalogueException.Conflict("Settings were not saved before. Reload.");
            record.Revision = 1;
            db.Settings.Add(record);
        }
        else
        {
            if (current.Revision != expectedRevision) throw CatalogueException.Conflict("Settings were saved elsewhere. Reload before saving.");
            current.Revision += 1;
            current.Json = record.Json;
            record = current;
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        db.ChangeTracker.Clear();
        return record;
    }

    private static ProductRevisionRecord Snapshot(ProductRecord record, string summary) => new()
    {
        Id = Guid.NewGuid(),
        ProductId = record.Id,
        Revision = record.Revision,
        SavedAt = record.UpdatedAt,
        Summary = summary,
        Json = record.Json,
    };
}
