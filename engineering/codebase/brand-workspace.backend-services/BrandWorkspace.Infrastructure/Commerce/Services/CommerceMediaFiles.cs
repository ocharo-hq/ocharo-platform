using System.Security.Cryptography;
using BrandWorkspace.Application.Commerce.Repositories;
using BrandWorkspace.Application.Files.Constants;
using BrandWorkspace.Application.Files.Models;
using BrandWorkspace.Application.Files.Services;
using BrandWorkspace.Application.Preparation.Models;
using BrandWorkspace.Application.Preparation.Repositories;
using BrandWorkspace.Domain.Commerce;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace BrandWorkspace.Infrastructure.Commerce.Services;

/// <summary>
/// Puts a brand's media in WoW2 Files before the draft's transaction stores their rows, and serves them back
/// (planning/files-adoption.md #9, #11, #13). Each file carries <c>desk.commerce-media:{brand_id}/{sha256}</c>; a
/// refused transaction leaves it in Files, still referenced, and the next attempt reuses it. Without Files every blob
/// keeps its bytes for PostgreSQL, as before.
/// </summary>
public sealed class CommerceMediaFiles(
    ICommerceRepository commerce,
    IPreparationRepository preparation,
    IDeskFileStore files)
{
    /// <summary>
    /// Stores each blob in Files once, unless the brand already holds that file, and hands back blobs that carry only
    /// the file id; <paramref name="names"/> names a file as a download saves it.
    /// </summary>
    public async Task<IReadOnlyList<CommerceBlob>> StoreAsync(
        Guid brandId,
        IReadOnlyList<CommerceBlob> blobs,
        int maximumBytes,
        IReadOnlyDictionary<string, string>? names,
        CancellationToken token)
    {
        if (!files.IsEnabled) return blobs;
        var stored = new List<CommerceBlob>(blobs.Count);
        foreach (var blob in blobs.DistinctBy(blob => blob.Sha256))
        {
            if (blob.FileId is not null)
            {
                stored.Add(blob);
                continue;
            }
            if (await commerce.MediaFileAsync(brandId, blob.Sha256, token) is { } known)
            {
                stored.Add(blob with { Bytes = [], FileId = known });
                continue;
            }
            CommerceRules.Require(
                blob.Bytes.Length > 0
                && blob.Bytes.Length <= maximumBytes
                && blob.Sha256 == Convert.ToHexStringLower(SHA256.HashData(blob.Bytes)),
                "Imported media bytes do not match their hash.");
            var name = names?.GetValueOrDefault(blob.Sha256) ?? DeskFiles.Name(blob.Sha256, blob.ContentType);
            var file = await files.StoreAsync(
                blob.Bytes,
                name,
                blob.ContentType,
                DeskFileReference.CommerceMedia(brandId, blob.Sha256),
                token);
            stored.Add(blob with { Bytes = [], FileId = file.Id });
        }
        return stored;
    }

    /// <summary>
    /// Takes an operator's preparation image into the brand (#11): with Files the commerce row references the
    /// preparation file instead of copying its bytes; an image stored before Files that the backfill has not reached
    /// yet uploads its bytes once under the commerce reference. Not found when the organization lacks the image.
    /// </summary>
    public async Task<Result<CommerceBlob>> FromPreparationAsync(
        Guid organizationId,
        Guid brandId,
        Guid mediaId,
        CancellationToken token)
    {
        if (!files.IsEnabled)
        {
            Result<MediaContent> content = await preparation.GetMediaContentAsync(organizationId, mediaId, token);
            return content.Map(read => new CommerceBlob(read.Asset.Sha256, read.Asset.ContentType, read.Bytes));
        }
        Result<PreparationMediaFile> found = await preparation.GetMediaFileAsync(organizationId, mediaId, token);
        if (found.IsFailure(out AppError? error, out var media)) return Result<CommerceBlob>.Fail(error);
        var (sha, type) = (media.Asset.Sha256, media.Asset.ContentType);
        if (await commerce.MediaFileAsync(brandId, sha, token) is { } known)
            return Result<CommerceBlob>.Ok(new CommerceBlob(sha, type, [], known));
        var reference = DeskFileReference.CommerceMedia(brandId, sha);
        if (media.FileId is { } fileId)
        {
            await files.AttachAsync(fileId, reference, token);
            return Result<CommerceBlob>.Ok(new CommerceBlob(sha, type, [], fileId));
        }
        Result<MediaContent> stored = await preparation.GetMediaContentAsync(organizationId, mediaId, token);
        if (stored.IsFailure(out AppError? missing, out var original)) return Result<CommerceBlob>.Fail(missing);
        var file = await files.StoreAsync(original.Bytes, DeskFiles.Name(sha, type), type, reference, token);
        return Result<CommerceBlob>.Ok(new CommerceBlob(sha, type, [], file.Id));
    }

    /// <summary>Reads a brand's media bytes, for a route the desk fetch()es into a blob.</summary>
    public async Task<StoredContent?> ReadAsync(Guid brandId, string sha256, CancellationToken token)
    {
        if (await commerce.BlobAsync(brandId, sha256, token) is not { } blob) return null;
        var bytes = blob.Bytes.Length > 0 ? blob.Bytes : await files.ReadAsync(blob.FileId!, sha256, token);
        return new StoredContent(blob.ContentType, null, bytes);
    }

    /// <summary>
    /// Opens a brand's media for a route a browser loads directly: a Files link when Files holds it, else the bytes.
    /// </summary>
    public async Task<StoredContent?> OpenAsync(Guid brandId, string sha256, bool download, CancellationToken token)
    {
        if (await commerce.BlobAsync(brandId, sha256, token) is not { } blob) return null;
        if (files.IsEnabled && blob.FileId is { } fileId)
            return new StoredContent(blob.ContentType, await files.LinkAsync(fileId, null, download, token), null);
        var bytes = blob.Bytes.Length > 0 ? blob.Bytes : await files.ReadAsync(blob.FileId!, sha256, token);
        return new StoredContent(blob.ContentType, null, bytes);
    }
}
