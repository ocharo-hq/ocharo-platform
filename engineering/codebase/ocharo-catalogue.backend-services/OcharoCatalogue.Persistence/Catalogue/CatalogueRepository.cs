using System.Data;
using Microsoft.EntityFrameworkCore;
using OcharoCatalogue.Application.Catalogue.Core.Constants;
using OcharoCatalogue.Application.Catalogue.Core.Errors;
using OcharoCatalogue.Application.Catalogue.Core.Repositories;
using OcharoCatalogue.Domain.Catalogue.Entities;

namespace OcharoCatalogue.Persistence.Catalogue;

/// <summary>Stores catalogue state in PostgreSQL, with revision checks enforced inside one serializable transaction; a
/// stale revision fails as a conflict, and a transaction another save overtook fails as busy.</summary>
public sealed class CatalogueRepository(OcharoCatalogueDbContext db) : ICatalogueRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<ProductRecord>> GetProductsAsync(CancellationToken ct)
    {
        return await db.Products.AsNoTracking().ToListAsync(ct);
    }

    /// <inheritdoc />
    public Task<ProductRecord?> FindProductAsync(string id, CancellationToken ct)
    {
        return db.Products.AsNoTracking().FirstOrDefaultAsync(product => product.Id == id, ct);
    }

    /// <inheritdoc />
    public async Task<ProductRecord> InsertProductAsync(
        ProductRecord record,
        string summary,
        CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (await db.Products.AnyAsync(product => product.Id == record.Id, ct))
        {
            throw Conflict($"A product with id '{record.Id}' already exists.");
        }

        db.Products.Add(record);
        db.ProductRevisions.Add(Snapshot(record, summary));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        db.ChangeTracker.Clear();
        return record;
    }

    /// <inheritdoc />
    public async Task<ProductRecord> UpdateProductAsync(
        ProductRecord record,
        int expectedRevision,
        string summary,
        CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var current = await db.Products.FirstOrDefaultAsync(product => product.Id == record.Id, ct)
            ?? throw CatalogueErrors.NotFound("Product").ToException();
        if (current.Revision != expectedRevision)
        {
            throw Conflict($"The product was saved elsewhere (now revision {current.Revision}). Reload before saving.");
        }

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

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProductRevisionRecord>> GetRevisionsAsync(string productId, CancellationToken ct)
    {
        return await db.ProductRevisions.AsNoTracking().Where(revision => revision.ProductId == productId).ToListAsync(
            ct);
    }

    /// <inheritdoc />
    public Task<ProductRevisionRecord?> FindRevisionAsync(
        string productId,
        int revision,
        CancellationToken ct)
    {
        return db.ProductRevisions.AsNoTracking()
            .FirstOrDefaultAsync(saved => saved.ProductId == productId && saved.Revision == revision, ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CollectionRecord>> GetCollectionsAsync(CancellationToken ct)
    {
        return await db.Collections.AsNoTracking().ToListAsync(ct);
    }

    /// <inheritdoc />
    public Task<CollectionRecord?> FindCollectionAsync(string id, CancellationToken ct)
    {
        return db.Collections.AsNoTracking().FirstOrDefaultAsync(collection => collection.Id == id, ct);
    }

    /// <inheritdoc />
    public async Task<CollectionRecord> SaveCollectionAsync(
        CollectionRecord record,
        int? expectedRevision,
        CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var current = await db.Collections.FirstOrDefaultAsync(collection => collection.Id == record.Id, ct);
        if (expectedRevision is null)
        {
            if (current is not null)
            {
                throw Conflict($"Collection '{record.Id}' already exists.");
            }

            record.Revision = 1;
            db.Collections.Add(record);
        }
        else
        {
            if (current is null)
            {
                throw CatalogueErrors.NotFound("Collection").ToException();
            }

            if (current.Revision != expectedRevision)
            {
                throw Conflict("The collection was saved elsewhere. Reload before saving.");
            }

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

    /// <inheritdoc />
    public async Task DeleteCollectionAsync(string id, CancellationToken ct)
    {
        await db.Collections.Where(collection => collection.Id == id).ExecuteDeleteAsync(ct);
    }

    /// <inheritdoc />
    public Task<MediaBlobRecord?> FindMediaAsync(string sha256, CancellationToken ct)
    {
        return db.MediaBlobs.AsNoTracking().FirstOrDefaultAsync(media => media.Sha256 == sha256, ct);
    }

    /// <inheritdoc />
    public async Task SaveMediaAsync(MediaBlobRecord record, CancellationToken ct)
    {
        if (await db.MediaBlobs.AnyAsync(media => media.Sha256 == record.Sha256, ct))
        {
            return;
        }

        db.MediaBlobs.Add(record);
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MediaBlobRecord>> GetMediaWithoutFileAsync(CancellationToken ct)
    {
        return await db.MediaBlobs.AsNoTracking().Where(media => media.FileId == null).ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task SetMediaFileAsync(
        string sha256,
        string fileId,
        CancellationToken ct)
    {
        await db.MediaBlobs
            .Where(media => media.Sha256 == sha256)
            .ExecuteUpdateAsync(set => set.SetProperty(media => media.FileId, fileId), ct);
        db.ChangeTracker.Clear();
    }

    /// <inheritdoc />
    public Task<SettingsRecord?> FindSettingsAsync(CancellationToken ct)
    {
        return db.Settings.AsNoTracking().FirstOrDefaultAsync(settings => settings.Id == CatalogueKeys.SettingsId, ct);
    }

    /// <inheritdoc />
    public async Task<SettingsRecord> SaveSettingsAsync(
        SettingsRecord record,
        int? expectedRevision,
        CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var current = await db.Settings.FirstOrDefaultAsync(settings => settings.Id == record.Id, ct);
        if (current is null)
        {
            if (expectedRevision is not null)
            {
                throw Conflict("Settings were not saved before. Reload.");
            }

            record.Revision = 1;
            db.Settings.Add(record);
        }
        else
        {
            if (current.Revision != expectedRevision)
            {
                throw Conflict("Settings were saved elsewhere. Reload before saving.");
            }

            current.Revision += 1;
            current.Json = record.Json;
            record = current;
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        db.ChangeTracker.Clear();
        return record;
    }

    private static ProductRevisionRecord Snapshot(ProductRecord record, string summary)
    {
        return new ProductRevisionRecord
        {
            Id = Guid.NewGuid(),
            ProductId = record.Id,
            Revision = record.Revision,
            SavedAt = record.UpdatedAt,
            Summary = summary,
            Json = record.Json,
        };
    }

    private static Exception Conflict(string message)
    {
        return CatalogueErrors.Conflict(message).ToException();
    }
}
