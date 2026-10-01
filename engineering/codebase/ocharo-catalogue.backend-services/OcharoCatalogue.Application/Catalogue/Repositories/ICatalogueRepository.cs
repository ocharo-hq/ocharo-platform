using OcharoCatalogue.Domain.Catalogue.Entities;

namespace OcharoCatalogue.Application.Catalogue.Repositories;

/// <summary>Durable catalogue state. Saves are revision-checked; a stale revision throws a 409.</summary>
public interface ICatalogueRepository
{
    Task<IReadOnlyList<ProductRecord>> GetProductsAsync(CancellationToken ct);
    Task<ProductRecord?> FindProductAsync(string id, CancellationToken ct);

    /// <summary>Inserts a new product at revision 1 together with its first revision snapshot.</summary>
    Task<ProductRecord> InsertProductAsync(ProductRecord record, string summary, CancellationToken ct);

    /// <summary>Replaces a product when its stored revision equals <paramref name="expectedRevision"/>; stores a snapshot.</summary>
    Task<ProductRecord> UpdateProductAsync(ProductRecord record, int expectedRevision, string summary, CancellationToken ct);

    Task<IReadOnlyList<ProductRevisionRecord>> GetRevisionsAsync(string productId, CancellationToken ct);
    Task<ProductRevisionRecord?> FindRevisionAsync(string productId, int revision, CancellationToken ct);

    Task<IReadOnlyList<CollectionRecord>> GetCollectionsAsync(CancellationToken ct);
    Task<CollectionRecord?> FindCollectionAsync(string id, CancellationToken ct);
    Task<CollectionRecord> SaveCollectionAsync(CollectionRecord record, int? expectedRevision, CancellationToken ct);
    Task DeleteCollectionAsync(string id, CancellationToken ct);

    Task<MediaBlobRecord?> FindMediaAsync(string sha256, CancellationToken ct);
    Task SaveMediaAsync(MediaBlobRecord record, CancellationToken ct);

    Task<SettingsRecord?> FindSettingsAsync(CancellationToken ct);
    Task<SettingsRecord> SaveSettingsAsync(SettingsRecord record, int? expectedRevision, CancellationToken ct);
}
