using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using OcharoCatalogue.Application.Catalogue.Core.Errors;
using OcharoCatalogue.Application.Catalogue.Core.Repositories;
using OcharoCatalogue.Application.Catalogue.Media.Enums;
using OcharoCatalogue.Application.Catalogue.Media.Models;
using OcharoCatalogue.Application.Catalogue.Media.Services;
using OcharoCatalogue.Domain.Catalogue.Entities;
using OcharoCatalogue.Infrastructure.Catalogue.Core.Options;
using OcharoCatalogue.Infrastructure.Catalogue.Media.Constants;
using OcharoCatalogue.Infrastructure.Catalogue.Media.Validators;
using WoW.Two.Sdk.Beta.Be.Files.References;
using WoW.Two.Sdk.Beta.Be.Files.Store;
using WoW.Two.Sdk.Beta.Be.Files.Uploads;
using WoW.Two.Sdk.Beta.Be.Files.Urls;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace OcharoCatalogue.Infrastructure.Catalogue.Media.Stores;

/// <summary>Stores originals in WoW2 Files once per SHA-256, and links to them for <see
/// cref="MediaContent.LinkLifetime"/>;
/// Files makes the WebP previews as variants. A file stored before Files reads from the local blob folder through
/// <see cref="MediaStore"/> until the migration fills its id (planning/files-adoption.md #24).</summary>
/// <remarks>A person's request calls Files with their exchanged token, any other call with Catalogue's own; the Files
/// client picks by whether the request has a signed-in session.</remarks>
public sealed class FilesMediaStore(
    ICatalogueRepository repository,
    IFileUploadsBroker uploads,
    IFileReferencesBroker references,
    IFileUrlsBroker urls,
    IFileStoreBroker store,
    MediaStore local,
    IOptions<CatalogueOptions> options,
    TimeProvider timeProvider) : IMediaStore
{
    /// <inheritdoc />
    public async Task<MediaBlobRecord> StoreAsync(byte[] bytes, CancellationToken ct)
    {
        var contentType = MediaBytes.Check(bytes, options.Value.MaximumImageBytes);
        var sha = Convert.ToHexStringLower(SHA256.HashData(bytes));
        var existing = await repository.FindMediaAsync(sha, ct);
        string? lost = null;
        if (existing is { FileId: not null } && FileId.TryParse(existing.FileId, out var known))
        {
            // A file deleted in Files (its API or console) is restored while its retention lasts; one already
            // purged, or past restoring, is uploaded anew under a key of its own, since the first key still names
            // the lost file.
            var current = await store.GetAsync(known, ct);
            if (current is Result<FileModel>.Success { Value.DeletedAtUtc: null })
            {
                return existing;
            }

            if (current is Result<FileModel>.Failure { Error.Type: not AppErrorType.NotFound } failure)
            {
                throw Refusal(failure.Error);
            }

            if (current is Result<FileModel>.Success
                && await store.RestoreAsync(known, ct) is Result<FileModel>.Success)
            {
                return existing;
            }

            lost = existing.FileId;
        }

        FileModel file;
        await using (var content = new MemoryStream(bytes, false))
        {
            var spec = new UploadSpec
            {
                Name = $"{sha[..16]}.{MediaBytes.Extension(contentType)}",
                ContentType = contentType,
                AppId = CatalogueFiles.AppId,
                IdempotencyKey = lost is null
                    ? $"{CatalogueFiles.MediaReference}:{sha}"
                    : $"{CatalogueFiles.MediaReference}:{sha}:{lost}",
            };
            file = Unwrap(await uploads.UploadAsync(
                content,
                spec,
                ct));
        }

        Unwrap(await references.AttachAsync(
            file.Id,
            new FileReference(CatalogueFiles.MediaReference, sha),
            ct));

        if (existing is not null)
        {
            await repository.SetMediaFileAsync(
                sha,
                file.Id.ToString(),
                ct);
            existing.FileId = file.Id.ToString();
            return existing;
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
            StoredAt = timeProvider.GetUtcNow(),
        };
        await repository.SaveMediaAsync(record, ct);
        return record;
    }

    /// <inheritdoc />
    public async Task<MediaContent?> OpenAsync(
        string sha256,
        bool preview,
        CancellationToken ct)
    {
        if (!MediaBytes.IsSha256(sha256))
        {
            return null;
        }

        var record = await repository.FindMediaAsync(sha256, ct);
        if (record is null)
        {
            return null;
        }

        if (record.FileId is null || !FileId.TryParse(record.FileId, out var id))
        {
            return await local.OpenLocalAsync(
                record,
                preview,
                ct);
        }

        var spec = new UrlSpec
        {
            Lifetime = MediaContent.LinkLifetime,
            Width = preview ? options.Value.PreviewSize : null,
        };
        var link = Unwrap(await urls.IssueAsync(
            id,
            spec,
            ct));
        return new MediaContent(
            record,
            preview ? "image/webp" : record.ContentType,
            link.Url,
            null);
    }

    /// <inheritdoc />
    public async Task<MediaCheck> CheckAsync(string sha256, CancellationToken ct)
    {
        if (!MediaBytes.IsSha256(sha256))
        {
            return MediaCheck.Missing;
        }

        var record = await repository.FindMediaAsync(sha256, ct);
        if (record is null)
        {
            return MediaCheck.Missing;
        }

        if (record.FileId is null || !FileId.TryParse(record.FileId, out var id))
        {
            return await local.CheckLocalAsync(record, ct);
        }

        // Files hashed the bytes on upload; the check confirms the file is still there, ready and the same.
        return await store.GetAsync(id, ct) switch
        {
            // Files answers a deleted file too, so a release never publishes an image its link would not serve.
            Result<FileModel>.Success { Value.DeletedAtUtc: not null } => MediaCheck.Missing,
            Result<FileModel>.Success { Value: { State: FileState.Ready } file } =>
                file.Sha256 == sha256 ? MediaCheck.Ok : MediaCheck.Corrupt,
            Result<FileModel>.Success => MediaCheck.Corrupt,
            Result<FileModel>.Failure { Error.Type: AppErrorType.NotFound } => MediaCheck.Missing,
            _ => MediaCheck.Unavailable,
        };
    }

    /// <summary>
    /// Returns a Files call's value, or refuses the Catalogue request with the error its failure maps to.
    /// </summary>
    /// <typeparam name="T">The call's value type.</typeparam>
    /// <param name="result">The Files call's result.</param>
    internal static T Unwrap<T>(Result<T> result) where T : notnull
    {
        return result.Match(value => value, error => throw Refusal(error));
    }

    /// <summary>Refuses the Catalogue request when a Files call without a value failed.</summary>
    /// <param name="result">The Files call's result.</param>
    internal static void Unwrap(Result result)
    {
        result.Match(() => true, error => throw Refusal(error));
    }

    /// <summary>
    /// Maps a Files failure to the catalogue error the request fails with, as an exception for the mediator.
    /// </summary>
    /// <param name="error">The Files failure.</param>
    internal static AppException Refusal(AppError error)
    {
        var refusal = error.Type switch
        {
            AppErrorType.Validation => CatalogueErrors.Invalid(error.Message),
            AppErrorType.NotFound => CatalogueErrors.Missing("The file is gone from WoW2 Files."),
            AppErrorType.ExternalUnavailable => CatalogueErrors.FilesUnavailable(
                "WoW2 Files is unavailable; try again shortly."),
            _ => CatalogueErrors.FilesRefused($"WoW2 Files refused the call: {error.Message}"),
        };
        return refusal.ToException();
    }
}
