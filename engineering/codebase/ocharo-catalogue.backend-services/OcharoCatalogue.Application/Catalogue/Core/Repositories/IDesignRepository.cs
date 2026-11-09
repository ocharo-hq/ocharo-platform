using OcharoCatalogue.Domain.Catalogue.Entities;

namespace OcharoCatalogue.Application.Catalogue.Core.Repositories;

/// <summary>Defines durable catalogue designs, their revisions, prepared releases and publications.</summary>
public interface IDesignRepository
{
    /// <summary>Gets the designs, archived ones included when <paramref name="archived"/> is set.</summary>
    /// <param name="archived">Whether archived designs are listed too.</param>
    /// <param name="ct">The token that cancels the read.</param>
    Task<IReadOnlyList<CatalogueDesignRecord>> ListAsync(bool archived, CancellationToken ct);

    /// <summary>Finds a design; null when none has <paramref name="id"/>.</summary>
    /// <param name="id">The design id.</param>
    /// <param name="ct">The token that cancels the read.</param>
    Task<CatalogueDesignRecord?> GetAsync(Guid id, CancellationToken ct);

    /// <summary>Saves a design with a new revision: an insert without <paramref name="expectedRevision"/>.</summary>
    /// <param name="record">The design row.</param>
    /// <param name="expectedRevision">The revision the caller edited; null for a new design.</param>
    /// <param name="ct">The token that cancels the write.</param>
    Task<CatalogueDesignRecord> SaveAsync(
        CatalogueDesignRecord record,
        int? expectedRevision,
        CancellationToken ct);

    /// <summary>Archives a design at <paramref name="expectedRevision"/> and drops its publications.</summary>
    /// <param name="id">The design id.</param>
    /// <param name="expectedRevision">The revision the caller archives.</param>
    /// <param name="now">The archive time.</param>
    /// <param name="ct">The token that cancels the write.</param>
    Task ArchiveAsync(
        Guid id,
        int expectedRevision,
        DateTimeOffset now,
        CancellationToken ct);

    /// <summary>Gets every saved revision of a design, newest first.</summary>
    /// <param name="id">The design id.</param>
    /// <param name="ct">The token that cancels the read.</param>
    Task<IReadOnlyList<CatalogueDesignRevisionRecord>> RevisionsAsync(Guid id, CancellationToken ct);

    /// <summary>Finds one saved revision of a design; null when it does not exist.</summary>
    /// <param name="id">The design id.</param>
    /// <param name="revision">The revision number.</param>
    /// <param name="ct">The token that cancels the read.</param>
    Task<CatalogueDesignRevisionRecord?> RevisionAsync(
        Guid id,
        int revision,
        CancellationToken ct);

    /// <summary>Gets the prepared releases of a design, newest first.</summary>
    /// <param name="designId">The design id.</param>
    /// <param name="ct">The token that cancels the read.</param>
    Task<IReadOnlyList<CatalogueReleaseRecord>> ReleasesAsync(Guid designId, CancellationToken ct);

    /// <summary>Finds a prepared release; null when none has <paramref name="releaseId"/>.</summary>
    /// <param name="releaseId">The release id.</param>
    /// <param name="ct">The token that cancels the read.</param>
    Task<CatalogueReleaseRecord?> ReleaseAsync(Guid releaseId, CancellationToken ct);

    /// <summary>
    /// Saves a prepared release with its artifacts: rows whose bytes SQLite keeps, and files WoW2 Files holds.
    /// </summary>
    /// <param name="release">The release row.</param>
    /// <param name="assets">The artifacts SQLite keeps.</param>
    /// <param name="files">The artifacts WoW2 Files holds.</param>
    /// <param name="ct">The token that cancels the write.</param>
    Task PrepareAsync(
        CatalogueReleaseRecord release,
        IReadOnlyList<CatalogueReleaseAssetRecord> assets,
        IReadOnlyList<CatalogueReleaseFileRecord> files,
        CancellationToken ct);

    /// <summary>Publishes a prepared release at the design's <paramref name="expectedRevision"/>.</summary>
    /// <param name="designId">The design id.</param>
    /// <param name="releaseId">The release id.</param>
    /// <param name="expectedRevision">The design revision the release was prepared from.</param>
    /// <param name="now">The publication time.</param>
    /// <param name="ct">The token that cancels the write.</param>
    Task<CatalogueReleaseRecord> PublishAsync(
        Guid designId,
        Guid releaseId,
        int expectedRevision,
        DateTimeOffset now,
        CancellationToken ct);

    /// <summary>Withdraws every published release of a design at <paramref name="expectedRevision"/>.</summary>
    /// <param name="designId">The design id.</param>
    /// <param name="expectedRevision">The design revision the caller withdraws.</param>
    /// <param name="now">The withdrawal time.</param>
    /// <param name="ct">The token that cancels the write.</param>
    Task WithdrawAsync(
        Guid designId,
        int expectedRevision,
        DateTimeOffset now,
        CancellationToken ct);

    /// <summary>
    /// Finds the published release of a slug and language, or the pinned one; null when none is readable.
    /// </summary>
    /// <param name="slug">The catalogue slug.</param>
    /// <param name="locale">The catalogue language.</param>
    /// <param name="releaseId">The pinned release; null for the current one.</param>
    /// <param name="ct">The token that cancels the read.</param>
    Task<CatalogueReleaseRecord?> PublishedAsync(
        string slug,
        string locale,
        Guid? releaseId,
        CancellationToken ct);

    /// <summary>Gets the current publications.</summary>
    /// <param name="ct">The token that cancels the read.</param>
    Task<IReadOnlyList<CatalogueReleaseRecord>> PublicationsAsync(CancellationToken ct);

    /// <summary>Gets the current and pinned historical releases that remain publicly accessible.</summary>
    /// <param name="ct">The token that cancels the read.</param>
    Task<IReadOnlyList<CatalogueReleaseRecord>> PublicReleasesAsync(CancellationToken ct);

    /// <summary>Finds an artifact whose bytes SQLite holds; null when unknown.</summary>
    /// <param name="sha256">The artifact's SHA-256.</param>
    /// <param name="ct">The token that cancels the read.</param>
    Task<CatalogueReleaseAssetRecord?> AssetAsync(string sha256, CancellationToken ct);

    /// <summary>Finds the WoW2 Files id of a release artifact; null while SQLite holds its bytes alone.</summary>
    /// <param name="sha256">The artifact's SHA-256.</param>
    /// <param name="ct">The token that cancels the read.</param>
    Task<CatalogueReleaseFileRecord?> ReleaseFileAsync(string sha256, CancellationToken ct);

    /// <summary>Lists the artifacts SQLite holds with no Files id yet, as SHA-256 to content type.</summary>
    /// <param name="ct">The token that cancels the read.</param>
    Task<IReadOnlyDictionary<string, string>> AssetsWithoutFileAsync(CancellationToken ct);

    /// <summary>Records the Files id of an artifact the migration moved; a known one stays as it is.</summary>
    /// <param name="file">The artifact's Files record.</param>
    /// <param name="ct">The token that cancels the write.</param>
    Task SaveReleaseFileAsync(CatalogueReleaseFileRecord file, CancellationToken ct);
}
