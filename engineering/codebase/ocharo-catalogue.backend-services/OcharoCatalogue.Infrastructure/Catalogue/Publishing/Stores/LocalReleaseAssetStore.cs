using OcharoCatalogue.Application.Catalogue.Publishing.Models;
using OcharoCatalogue.Application.Catalogue.Publishing.Services;
using OcharoCatalogue.Domain.Catalogue.Entities;

namespace OcharoCatalogue.Infrastructure.Catalogue.Publishing.Stores;

/// <summary>
/// Keeps release artifacts in SQLite's <c>catalogue_release_assets</c>: a run with <c>Files:BaseUrl</c> unset.
/// </summary>
public sealed class LocalReleaseAssetStore : IReleaseAssetStore
{
    /// <inheritdoc />
    public Task<ReleaseAssetStorage> StoreAsync(IReadOnlyList<ReleaseArtifact> artifacts, CancellationToken ct)
    {
        var rows = artifacts
            .Select(artifact => new CatalogueReleaseAssetRecord
            {
                Sha256 = artifact.Sha256,
                ContentType = artifact.ContentType,
                Bytes = artifact.Bytes,
            })
            .ToArray();
        return Task.FromResult(new ReleaseAssetStorage(rows, []));
    }

    /// <inheritdoc />
    public Task<Uri?> LinkAsync(string sha256, CancellationToken ct)
    {
        return Task.FromResult<Uri?>(null);
    }
}
