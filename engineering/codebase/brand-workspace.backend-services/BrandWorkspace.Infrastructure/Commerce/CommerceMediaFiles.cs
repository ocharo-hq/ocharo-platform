using System.Security.Cryptography;
using BrandWorkspace.Application.Commerce;
using BrandWorkspace.Application.Files;
using BrandWorkspace.Application.Preparation.Models;
using BrandWorkspace.Application.Preparation.Repositories;
using BrandWorkspace.Domain.Commerce;

namespace BrandWorkspace.Infrastructure.Commerce;

/// <summary>Puts a brand's media in WoW2 Files before the draft's transaction stores their rows, and serves them back
/// (planning/files-adoption.md #9, #11, #13). Each file carries <c>desk.commerce-media:{brand_id}/{sha256}</c>; a refused
/// transaction leaves it in Files, still referenced, and the next attempt reuses it. Without Files every blob keeps its
/// bytes for PostgreSQL, as before.</summary>
public sealed class CommerceMediaFiles(ICommerceRepository commerce, IPreparationRepository preparation, IDeskFileStore files)
{
    /// <summary>Stores each blob in Files once, unless the brand already holds that file, and hands back blobs that carry
    /// only the file id; <paramref name="names"/> names a file as a download saves it.</summary>
    public async Task<IReadOnlyList<CommerceBlob>> StoreAsync(Guid brandId, IReadOnlyList<CommerceBlob> blobs, int maximumBytes,
        IReadOnlyDictionary<string, string>? names, CancellationToken token)
    {
        if (!files.IsEnabled) return blobs;
        var stored = new List<CommerceBlob>(blobs.Count);
        foreach (var blob in blobs.DistinctBy(blob => blob.Sha256))
        {
            if (blob.FileId is not null) { stored.Add(blob); continue; }
            if (await commerce.MediaFileAsync(brandId, blob.Sha256, token) is { } known)
            {
                stored.Add(blob with { Bytes = [], FileId = known });
                continue;
            }
            CommerceRules.Require(blob.Bytes.Length > 0 && blob.Bytes.Length <= maximumBytes
                && blob.Sha256 == Convert.ToHexStringLower(SHA256.HashData(blob.Bytes)), "Imported media bytes do not match their hash.");
            var name = names?.GetValueOrDefault(blob.Sha256) ?? DeskFiles.Name(blob.Sha256, blob.ContentType);
            var file = await files.StoreAsync(blob.Bytes, name, blob.ContentType, DeskFileReference.CommerceMedia(brandId, blob.Sha256), token);
            stored.Add(blob with { Bytes = [], FileId = file.Id });
        }
        return stored;
    }

    /// <summary>Takes an operator's preparation image into the brand (#11): with Files the commerce row references the
    /// preparation file instead of copying its bytes; an image stored before Files that the backfill has not reached yet
    /// uploads its bytes once under the commerce reference.</summary>
    public async Task<CommerceBlob> FromPreparationAsync(Guid organizationId, Guid brandId, Guid mediaId, CancellationToken token)
    {
        if (!files.IsEnabled)
        {
            var content = await preparation.GetMediaContentAsync(organizationId, mediaId, token);
            return new CommerceBlob(content.Asset.Sha256, content.Asset.ContentType, content.Bytes);
        }
        var media = await preparation.GetMediaFileAsync(organizationId, mediaId, token);
        var (sha, type) = (media.Asset.Sha256, media.Asset.ContentType);
        if (await commerce.MediaFileAsync(brandId, sha, token) is { } known) return new CommerceBlob(sha, type, [], known);
        var reference = DeskFileReference.CommerceMedia(brandId, sha);
        if (media.FileId is { } fileId)
        {
            await files.AttachAsync(fileId, reference, token);
            return new CommerceBlob(sha, type, [], fileId);
        }
        var bytes = (await preparation.GetMediaContentAsync(organizationId, mediaId, token)).Bytes;
        var file = await files.StoreAsync(bytes, DeskFiles.Name(sha, type), type, reference, token);
        return new CommerceBlob(sha, type, [], file.Id);
    }

    /// <summary>Reads a brand's media bytes, for a route the desk fetch()es into a blob.</summary>
    public async Task<StoredContent?> ReadAsync(Guid brandId, string sha256, CancellationToken token)
    {
        if (await commerce.BlobAsync(brandId, sha256, token) is not { } blob) return null;
        return new StoredContent(blob.ContentType, null, blob.Bytes.Length > 0 ? blob.Bytes : await files.ReadAsync(blob.FileId!, sha256, token));
    }

    /// <summary>Opens a brand's media for a route a browser loads directly: a Files link when Files holds it, else the bytes.</summary>
    public async Task<StoredContent?> OpenAsync(Guid brandId, string sha256, bool download, CancellationToken token)
    {
        if (await commerce.BlobAsync(brandId, sha256, token) is not { } blob) return null;
        if (files.IsEnabled && blob.FileId is { } fileId)
            return new StoredContent(blob.ContentType, await files.LinkAsync(fileId, null, download, token), null);
        return new StoredContent(blob.ContentType, null, blob.Bytes.Length > 0 ? blob.Bytes : await files.ReadAsync(blob.FileId!, sha256, token));
    }
}
