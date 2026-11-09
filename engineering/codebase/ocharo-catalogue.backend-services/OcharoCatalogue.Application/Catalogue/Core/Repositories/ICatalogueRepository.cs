using OcharoCatalogue.Domain.Catalogue.Entities;

namespace OcharoCatalogue.Application.Catalogue.Core.Repositories;

/// <summary>
/// Defines durable catalogue state; saves are revision-checked, and a stale revision fails as a conflict.
/// </summary>
public interface ICatalogueRepository
{
    /// <summary>Gets every product row.</summary>
    /// <param name="ct">The token that cancels the read.</param>
    Task<IReadOnlyList<ProductRecord>> GetProductsAsync(CancellationToken ct);

    /// <summary>Finds a product row; null when none has <paramref name="id"/>.</summary>
    /// <param name="id">The product id.</param>
    /// <param name="ct">The token that cancels the read.</param>
    Task<ProductRecord?> FindProductAsync(string id, CancellationToken ct);

    /// <summary>Inserts a new product at revision 1 together with its first revision snapshot.</summary>
    /// <param name="record">The product row.</param>
    /// <param name="summary">The history line of the first revision.</param>
    /// <param name="ct">The token that cancels the write.</param>
    Task<ProductRecord> InsertProductAsync(
        ProductRecord record,
        string summary,
        CancellationToken ct);

    /// <summary>
    /// Replaces a product when its stored revision equals <paramref name="expectedRevision"/>; stores a snapshot.
    /// </summary>
    /// <param name="record">The new product row.</param>
    /// <param name="expectedRevision">The revision the caller edited.</param>
    /// <param name="summary">The history line of the new revision.</param>
    /// <param name="ct">The token that cancels the write.</param>
    Task<ProductRecord> UpdateProductAsync(
        ProductRecord record,
        int expectedRevision,
        string summary,
        CancellationToken ct);

    /// <summary>Gets every saved revision of a product.</summary>
    /// <param name="productId">The product id.</param>
    /// <param name="ct">The token that cancels the read.</param>
    Task<IReadOnlyList<ProductRevisionRecord>> GetRevisionsAsync(string productId, CancellationToken ct);

    /// <summary>Finds one saved revision of a product; null when it does not exist.</summary>
    /// <param name="productId">The product id.</param>
    /// <param name="revision">The revision number.</param>
    /// <param name="ct">The token that cancels the read.</param>
    Task<ProductRevisionRecord?> FindRevisionAsync(
        string productId,
        int revision,
        CancellationToken ct);

    /// <summary>Gets every collection row.</summary>
    /// <param name="ct">The token that cancels the read.</param>
    Task<IReadOnlyList<CollectionRecord>> GetCollectionsAsync(CancellationToken ct);

    /// <summary>Finds a collection row; null when none has <paramref name="id"/>.</summary>
    /// <param name="id">The collection id.</param>
    /// <param name="ct">The token that cancels the read.</param>
    Task<CollectionRecord?> FindCollectionAsync(string id, CancellationToken ct);

    /// <summary>
    /// Saves a collection: an insert without <paramref name="expectedRevision"/>, else a revision-checked update.
    /// </summary>
    /// <param name="record">The collection row.</param>
    /// <param name="expectedRevision">The revision the caller edited; null for a new collection.</param>
    /// <param name="ct">The token that cancels the write.</param>
    Task<CollectionRecord> SaveCollectionAsync(
        CollectionRecord record,
        int? expectedRevision,
        CancellationToken ct);

    /// <summary>Deletes a collection row.</summary>
    /// <param name="id">The collection id.</param>
    /// <param name="ct">The token that cancels the write.</param>
    Task DeleteCollectionAsync(string id, CancellationToken ct);

    /// <summary>Finds a stored file by its SHA-256; null when unknown.</summary>
    /// <param name="sha256">The file's SHA-256.</param>
    /// <param name="ct">The token that cancels the read.</param>
    Task<MediaBlobRecord?> FindMediaAsync(string sha256, CancellationToken ct);

    /// <summary>Records a stored file once per SHA-256.</summary>
    /// <param name="record">The stored file.</param>
    /// <param name="ct">The token that cancels the write.</param>
    Task SaveMediaAsync(MediaBlobRecord record, CancellationToken ct);

    /// <summary>Gets the files stored before Files, which still have no file id.</summary>
    /// <param name="ct">The token that cancels the read.</param>
    Task<IReadOnlyList<MediaBlobRecord>> GetMediaWithoutFileAsync(CancellationToken ct);

    /// <summary>Records the Files file that holds a stored file's bytes.</summary>
    /// <param name="sha256">The stored file's SHA-256.</param>
    /// <param name="fileId">The Files file id.</param>
    /// <param name="ct">The token that cancels the write.</param>
    Task SetMediaFileAsync(
        string sha256,
        string fileId,
        CancellationToken ct);

    /// <summary>Finds the settings row; null until settings are first saved.</summary>
    /// <param name="ct">The token that cancels the read.</param>
    Task<SettingsRecord?> FindSettingsAsync(CancellationToken ct);

    /// <summary>
    /// Saves the settings row: an insert without <paramref name="expectedRevision"/>, else revision-checked.
    /// </summary>
    /// <param name="record">The settings row.</param>
    /// <param name="expectedRevision">The revision the caller edited; null for the first save.</param>
    /// <param name="ct">The token that cancels the write.</param>
    Task<SettingsRecord> SaveSettingsAsync(
        SettingsRecord record,
        int? expectedRevision,
        CancellationToken ct);
}
