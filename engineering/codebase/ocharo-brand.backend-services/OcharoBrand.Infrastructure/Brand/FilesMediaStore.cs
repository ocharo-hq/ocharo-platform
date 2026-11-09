using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using OcharoBrand.Application.Brand.Errors;
using OcharoBrand.Application.Brand.Models;
using OcharoBrand.Application.Brand.Repositories;
using OcharoBrand.Application.Brand.Services;
using OcharoBrand.Domain.Brand;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Files;
using WoW.Two.Sdk.Beta.Be.Files.References;
using WoW.Two.Sdk.Beta.Be.Files.Store;
using WoW.Two.Sdk.Beta.Be.Files.Uploads;
using WoW.Two.Sdk.Beta.Be.Files.Urls;

namespace OcharoBrand.Infrastructure.Brand;

/// <summary>Stores originals in WoW2 Files once per SHA-256, and links to them for <see cref="MediaContent.LinkLifetime"/>;
/// Files makes the WebP previews as variants. A file stored before Files reads from the local blob folder through
/// <see cref="MediaStore"/> until the migration fills its id (planning/files-adoption.md #23).</summary>
/// <remarks>A person's request calls Files with their exchanged token, any other call with Brand's own; the Files
/// client picks by whether the request has a signed-in session.</remarks>
public sealed class FilesMediaStore(
    IBrandRepository repository,
    IFileUploadsBroker uploads,
    IFileReferencesBroker references,
    IFileUrlsBroker urls,
    IFileStoreBroker store,
    MediaStore local,
    IOptions<BrandOptions> options,
    TimeProvider time) : IMediaStore
{
    public async Task<Result<MediaBlobRecord>> StoreAsync(byte[] bytes, CancellationToken ct)
    {
        if (MediaBytes.Check(bytes, options.Value.MaximumImageBytes).IsFailure(out var refused, out var contentType))
            return Result<MediaBlobRecord>.Fail(refused);
        var sha = Convert.ToHexStringLower(SHA256.HashData(bytes));
        var existing = await repository.FindMediaAsync(sha, ct);
        string? lost = null;
        if (existing is { FileId: not null } && FileId.TryParse(existing.FileId, out var known))
        {
            // A file deleted in Files (its API or console) is restored while its retention lasts; one already purged, or
            // past restoring, is uploaded anew under a key of its own, since the first key still names the lost file.
            var current = await store.GetAsync(known, ct);
            if (current is Result<FileModel>.Success { Value.DeletedAtUtc: null }) return Result<MediaBlobRecord>.Ok(existing);
            if (current is Result<FileModel>.Failure { Error.Type: not AppErrorType.NotFound } failure)
                return Result<MediaBlobRecord>.Fail(Refusal(failure.Error));
            if (current is Result<FileModel>.Success && await store.RestoreAsync(known, ct) is Result<FileModel>.Success)
                return Result<MediaBlobRecord>.Ok(existing);
            lost = existing.FileId;
        }

        FileModel file;
        await using (var content = new MemoryStream(bytes, false))
        {
            var spec = new UploadSpec
            {
                Name = $"{sha[..16]}.{MediaBytes.Extension(contentType)}",
                ContentType = contentType,
                AppId = BrandFiles.AppId,
                IdempotencyKey = lost is null ? $"{BrandFiles.MediaReference}:{sha}" : $"{BrandFiles.MediaReference}:{sha}:{lost}",
            };
            if ((await uploads.UploadAsync(content, spec, ct)).IsFailure(out var uploadError, out var uploaded))
                return Result<MediaBlobRecord>.Fail(Refusal(uploadError));
            file = uploaded;
        }
        if (await references.AttachAsync(file.Id, new FileReference(BrandFiles.MediaReference, sha), ct) is Result.Failure attachFailure)
            return Result<MediaBlobRecord>.Fail(Refusal(attachFailure.Error));

        if (existing is not null)
        {
            await repository.SetMediaFileAsync(sha, file.Id.ToString(), ct);
            existing.FileId = file.Id.ToString();
            return Result<MediaBlobRecord>.Ok(existing);
        }

        var (width, height) = MediaBytes.Dimensions(bytes);
        var record = new MediaBlobRecord
        {
            Sha256 = sha,
            ContentType = contentType,
            ByteLength = bytes.LongLength,
            FileId = file.Id.ToString(),
            Width = width,
            Height = height,
            StoredAt = time.GetUtcNow(),
        };
        await repository.SaveMediaAsync(record, ct);
        return Result<MediaBlobRecord>.Ok(record);
    }

    public async Task<Result<MediaContent>> OpenAsync(string sha256, bool preview, CancellationToken ct)
    {
        var record = MediaBytes.IsSha256(sha256) ? await repository.FindMediaAsync(sha256, ct) : null;
        if (record is null)
            return Result<MediaContent>.Fail(BrandErrors.NotFound("Media"));
        if (record.FileId is null || !FileId.TryParse(record.FileId, out var id))
        {
            var stored = await local.OpenLocalAsync(record, preview, ct);
            return stored is null ? Result<MediaContent>.Fail(BrandErrors.NotFound("Media")) : Result<MediaContent>.Ok(stored);
        }

        var spec = new UrlSpec { Lifetime = MediaContent.LinkLifetime, Width = preview ? options.Value.PreviewSize : null };
        if ((await urls.IssueAsync(id, spec, ct)).IsFailure(out var error, out var link))
            return Result<MediaContent>.Fail(Refusal(error));
        return Result<MediaContent>.Ok(new MediaContent(record, preview ? "image/webp" : record.ContentType, link.Url, null));
    }

    /// <summary>The Brand error a Files failure refuses the request with.</summary>
    private static AppError Refusal(AppError error) => error.Type switch
    {
        AppErrorType.Validation => BrandErrors.Invalid(error.Message),
        AppErrorType.NotFound => BrandErrors.FilesGone(),
        AppErrorType.ExternalUnavailable => BrandErrors.FilesUnavailable(),
        _ => BrandErrors.FilesRefused(error.Message),
    };
}
