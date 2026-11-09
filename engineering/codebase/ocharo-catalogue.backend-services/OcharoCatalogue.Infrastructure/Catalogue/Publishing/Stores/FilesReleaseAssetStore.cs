using OcharoCatalogue.Application.Catalogue.Core.Repositories;
using OcharoCatalogue.Application.Catalogue.Media.Models;
using OcharoCatalogue.Application.Catalogue.Publishing.Models;
using OcharoCatalogue.Application.Catalogue.Publishing.Services;
using OcharoCatalogue.Domain.Catalogue.Entities;
using OcharoCatalogue.Infrastructure.Catalogue.Media.Constants;
using OcharoCatalogue.Infrastructure.Catalogue.Media.Stores;
using WoW.Two.Sdk.Beta.Be.Files.References;
using WoW.Two.Sdk.Beta.Be.Files.Store;
using WoW.Two.Sdk.Beta.Be.Files.Uploads;
using WoW.Two.Sdk.Beta.Be.Files.Urls;

namespace OcharoCatalogue.Infrastructure.Catalogue.Publishing.Stores;

/// <summary>Stores release artifacts in WoW2 Files once per SHA-256, as app <c>catalogue</c> with reference
/// <c>catalogue.release:{sha}</c>, and links to them saved under their name; SQLite keeps only their file ids. An
/// artifact
/// prepared before Files reads its SQLite bytes until the migration fills its id (research/files-adoption.md
/// #26).</summary>
/// <remarks>Preparing is a member's request, so the upload goes with their exchanged token; the anonymous public route
/// links with Catalogue's own.</remarks>
public sealed class FilesReleaseAssetStore(
    IDesignRepository designs,
    IFileUploadsBroker uploads,
    IFileReferencesBroker references,
    IFileUrlsBroker urls) : IReleaseAssetStore
{
    /// <inheritdoc />
    public async Task<ReleaseAssetStorage> StoreAsync(IReadOnlyList<ReleaseArtifact> artifacts, CancellationToken ct)
    {
        var files = new List<CatalogueReleaseFileRecord>();
        foreach (var artifact in artifacts.DistinctBy(artifact => artifact.Sha256))
        {
            if (await designs.ReleaseFileAsync(artifact.Sha256, ct) is { } known)
            {
                files.Add(known);
                continue;
            }

            FileModel file;
            await using (var content = new MemoryStream(artifact.Bytes, false))
            {
                var spec = new UploadSpec
                {
                    Name = artifact.FileName,
                    ContentType = artifact.ContentType,
                    AppId = CatalogueFiles.AppId,
                    IdempotencyKey = $"{CatalogueFiles.ReleaseReference}:{artifact.Sha256}",
                };
                file = FilesMediaStore.Unwrap(await uploads.UploadAsync(
                    content,
                    spec,
                    ct));
            }

            FilesMediaStore.Unwrap(await references.AttachAsync(
                file.Id,
                new FileReference(CatalogueFiles.ReleaseReference, artifact.Sha256),
                ct));
            files.Add(new CatalogueReleaseFileRecord
            {
                Sha256 = artifact.Sha256,
                ContentType = artifact.ContentType,
                FileId = file.Id.ToString(),
            });
        }

        return new ReleaseAssetStorage([], files);
    }

    /// <inheritdoc />
    public async Task<Uri?> LinkAsync(string sha256, CancellationToken ct)
    {
        if (await designs.ReleaseFileAsync(sha256, ct) is not { } record || !FileId.TryParse(record.FileId, out var id))
        {
            return null;
        }

        var spec = new UrlSpec { Lifetime = MediaContent.LinkLifetime, Disposition = FileDisposition.Attachment };
        return FilesMediaStore.Unwrap(await urls.IssueAsync(
            id,
            spec,
            ct)).Url;
    }
}
