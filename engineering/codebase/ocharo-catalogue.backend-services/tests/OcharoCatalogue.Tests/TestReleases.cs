using OcharoCatalogue.Application.Catalogue.Publishing.Commands;
using OcharoCatalogue.Application.Catalogue.Publishing.Models;
using OcharoCatalogue.Application.Catalogue.Publishing.Queries;

namespace OcharoCatalogue.Tests;

/// <summary>Release commands and queries, the public editions included.</summary>
public sealed class TestReleases(CatalogueClient client)
{
    public Task<ReleasePreparation> PreparationAsync(Guid id, CancellationToken ct) => client.SendAsync(new ReleasePreparationGetQuery { DesignId = id }, ct);
    public Task<CataloguePreparedRelease> PrepareAsync(Guid id, int expected, string fingerprint, IReadOnlyDictionary<string, byte[]> files, CancellationToken ct) =>
        client.SendAsync(new ReleasePrepareCommand
        {
            DesignId = id,
            Metadata = new ReleasePrepareMetadata(expected, fingerprint),
            Files = files.Select(file => new ReleaseUpload(file.Key, file.Value)).ToArray(),
        }, ct);
    public Task<CatalogueReleaseSummary> PublishAsync(Guid designId, Guid releaseId, int expected, CancellationToken ct) =>
        client.SendAsync(new ReleasePublishCommand { DesignId = designId, ReleaseId = releaseId, ExpectedRevision = expected }, ct);
    public Task<Guid> WithdrawAsync(Guid designId, int expected, CancellationToken ct) => client.SendAsync(new ReleaseWithdrawCommand { DesignId = designId, ExpectedRevision = expected }, ct);
    public Task<IReadOnlyList<CatalogueReleaseSummary>> ReleasesAsync(Guid id, CancellationToken ct) => client.SendAsync(new ReleaseGetAllQuery { DesignId = id }, ct);
    public Task<PublishedEdition> PublicAsync(string slug, string? locale, Guid? releaseId, CancellationToken ct) =>
        client.SendAsync(new EditionGetBySlugQuery { Slug = slug, Locale = locale, ReleaseId = releaseId }, ct);
    public Task<IReadOnlyList<PublishedEditionSummary>> PublicListAsync(string? locale, CancellationToken ct) => client.SendAsync(new EditionGetAllQuery { Locale = locale }, ct);
    public Task<CataloguePublicAsset> PublicAssetAsync(string slug, string locale, Guid releaseId, string asset, CancellationToken ct) =>
        client.SendAsync(new EditionAssetGetQuery { Slug = slug, Locale = locale, ReleaseId = releaseId, Asset = asset }, ct);
}
