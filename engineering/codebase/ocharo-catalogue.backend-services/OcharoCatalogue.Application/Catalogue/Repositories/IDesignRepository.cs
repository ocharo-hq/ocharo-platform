using OcharoCatalogue.Domain.Catalogue.Entities;
namespace OcharoCatalogue.Application.Catalogue.Repositories;

public interface IDesignRepository
{
    Task<IReadOnlyList<CatalogueDesignRecord>> ListAsync(bool archived, CancellationToken ct);
    Task<CatalogueDesignRecord?> GetAsync(Guid id, CancellationToken ct);
    Task<CatalogueDesignRecord> SaveAsync(CatalogueDesignRecord record, int? expectedRevision, CancellationToken ct);
    Task ArchiveAsync(Guid id, int expectedRevision, DateTimeOffset now, CancellationToken ct);
    Task<IReadOnlyList<CatalogueDesignRevisionRecord>> RevisionsAsync(Guid id, CancellationToken ct);
    Task<CatalogueDesignRevisionRecord?> RevisionAsync(Guid id, int revision, CancellationToken ct);
    Task<IReadOnlyList<CatalogueReleaseRecord>> ReleasesAsync(Guid designId, CancellationToken ct);
    Task<CatalogueReleaseRecord?> ReleaseAsync(Guid releaseId, CancellationToken ct);
    /// <summary>Saves a prepared release with its artifacts: rows whose bytes SQLite keeps, and files WoW2 Files holds.</summary>
    Task PrepareAsync(CatalogueReleaseRecord release, IReadOnlyList<CatalogueReleaseAssetRecord> assets, IReadOnlyList<CatalogueReleaseFileRecord> files, CancellationToken ct);
    Task<CatalogueReleaseRecord> PublishAsync(Guid designId, Guid releaseId, int expectedRevision, DateTimeOffset now, CancellationToken ct);
    Task WithdrawAsync(Guid designId, int expectedRevision, DateTimeOffset now, CancellationToken ct);
    Task<CatalogueReleaseRecord?> PublishedAsync(string slug, string locale, Guid? releaseId, CancellationToken ct);
    Task<IReadOnlyList<CatalogueReleaseRecord>> PublicationsAsync(CancellationToken ct);
    /// <summary>Current and pinned historical releases that remain publicly accessible.</summary>
    Task<IReadOnlyList<CatalogueReleaseRecord>> PublicReleasesAsync(CancellationToken ct);
    Task<CatalogueReleaseAssetRecord?> AssetAsync(string sha256, CancellationToken ct);
    /// <summary>Finds the WoW2 Files id of a release artifact; null while SQLite holds its bytes alone.</summary>
    Task<CatalogueReleaseFileRecord?> ReleaseFileAsync(string sha256, CancellationToken ct);
    /// <summary>Lists the artifacts SQLite holds with no Files id yet, as SHA-256 to content type.</summary>
    Task<IReadOnlyDictionary<string, string>> AssetsWithoutFileAsync(CancellationToken ct);
    /// <summary>Records the Files id of an artifact the migration moved; a known one stays as it is.</summary>
    Task SaveReleaseFileAsync(CatalogueReleaseFileRecord file, CancellationToken ct);
}
