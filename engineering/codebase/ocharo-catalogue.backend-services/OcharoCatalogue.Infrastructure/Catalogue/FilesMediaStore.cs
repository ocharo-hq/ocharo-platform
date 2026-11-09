using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OcharoCatalogue.Application.Catalogue;
using OcharoCatalogue.Application.Catalogue.Repositories;
using OcharoCatalogue.Application.Catalogue.Services;
using OcharoCatalogue.Domain.Catalogue.Entities;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Files;
using WoW.Two.Sdk.Beta.Be.Files.References;
using WoW.Two.Sdk.Beta.Be.Files.Store;
using WoW.Two.Sdk.Beta.Be.Files.Uploads;
using WoW.Two.Sdk.Beta.Be.Files.Urls;

namespace OcharoCatalogue.Infrastructure.Catalogue;

/// <summary>The names Catalogue's files carry in WoW2 Files, the same the migration manifest gives the files it moves.</summary>
public static class CatalogueFiles
{
    /// <summary>The app every Catalogue file belongs to: Catalogue's Identity client id, so Files applies one policy.</summary>
    public const string AppId = "catalogue";

    /// <summary>The record type of the reference that keeps a stored file alive; its record id is the SHA-256.</summary>
    public const string MediaReference = "catalogue.media";

    /// <summary>The record type of a release artifact's reference, the same the manifest's <c>catalogue-release-assets</c>
    /// gives; its record id is the SHA-256.</summary>
    public const string ReleaseReference = "catalogue.release";
}

/// <summary>Stores originals in WoW2 Files once per SHA-256, and links to them for <see cref="MediaContent.LinkLifetime"/>;
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
    public async Task<MediaBlobRecord> StoreAsync(byte[] bytes, CancellationToken ct)
    {
        var contentType = MediaBytes.Check(bytes, options.Value.MaximumImageBytes);
        var sha = Convert.ToHexStringLower(SHA256.HashData(bytes));
        var existing = await repository.FindMediaAsync(sha, ct);
        string? lost = null;
        if (existing is { FileId: not null } && FileId.TryParse(existing.FileId, out var known))
        {
            // A file deleted in Files (its API or console) is restored while its retention lasts; one already purged, or
            // past restoring, is uploaded anew under a key of its own, since the first key still names the lost file.
            var current = await store.GetAsync(known, ct);
            if (current is Result<FileModel>.Success { Value.DeletedAtUtc: null }) return existing;
            if (current is Result<FileModel>.Failure { Error.Type: not AppErrorType.NotFound } failure) throw Refusal(failure.Error);
            if (current is Result<FileModel>.Success && await store.RestoreAsync(known, ct) is Result<FileModel>.Success) return existing;
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
                IdempotencyKey = lost is null ? $"{CatalogueFiles.MediaReference}:{sha}" : $"{CatalogueFiles.MediaReference}:{sha}:{lost}",
            };
            file = Unwrap(await uploads.UploadAsync(content, spec, ct));
        }
        Unwrap(await references.AttachAsync(file.Id, new FileReference(CatalogueFiles.MediaReference, sha), ct));

        if (existing is not null)
        {
            await repository.SetMediaFileAsync(sha, file.Id.ToString(), ct);
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

    public async Task<MediaContent?> OpenAsync(string sha256, bool preview, CancellationToken ct)
    {
        if (!MediaBytes.IsSha256(sha256)) return null;
        var record = await repository.FindMediaAsync(sha256, ct);
        if (record is null) return null;
        if (record.FileId is null || !FileId.TryParse(record.FileId, out var id))
            return await local.OpenLocalAsync(record, preview, ct);

        var spec = new UrlSpec { Lifetime = MediaContent.LinkLifetime, Width = preview ? options.Value.PreviewSize : null };
        var link = Unwrap(await urls.IssueAsync(id, spec, ct));
        return new MediaContent(record, preview ? "image/webp" : record.ContentType, link.Url, null);
    }

    public async Task<MediaCheck> CheckAsync(string sha256, CancellationToken ct)
    {
        if (!MediaBytes.IsSha256(sha256)) return MediaCheck.Missing;
        var record = await repository.FindMediaAsync(sha256, ct);
        if (record is null) return MediaCheck.Missing;
        if (record.FileId is null || !FileId.TryParse(record.FileId, out var id))
            return await local.CheckLocalAsync(record, ct);

        // Files hashed the bytes on upload; the check confirms the file is still there, ready and the same.
        return await store.GetAsync(id, ct) switch
        {
            // Files answers a deleted file too, so a release never publishes an image its link would not serve.
            Result<FileModel>.Success { Value.DeletedAtUtc: not null } => MediaCheck.Missing,
            Result<FileModel>.Success { Value: { State: FileState.Ready } file } => file.Sha256 == sha256 ? MediaCheck.Ok : MediaCheck.Corrupt,
            Result<FileModel>.Success => MediaCheck.Corrupt,
            Result<FileModel>.Failure { Error.Type: AppErrorType.NotFound } => MediaCheck.Missing,
            _ => MediaCheck.Unavailable,
        };
    }

    /// <summary>Returns a Files call's value, or refuses the Catalogue request with the status its failure maps to.</summary>
    internal static T Unwrap<T>(Result<T> result) where T : notnull => result.Match(value => value, error => throw Refusal(error));

    internal static void Unwrap(Result result) => result.Match(() => true, error => throw Refusal(error));

    internal static CatalogueException Refusal(AppError error) => error.Type switch
    {
        AppErrorType.Validation => new CatalogueException(error.Message, 400),
        AppErrorType.NotFound => new CatalogueException("The file is gone from WoW2 Files.", 404),
        AppErrorType.ExternalUnavailable => new CatalogueException("WoW2 Files is unavailable; try again shortly.", 503),
        _ => new CatalogueException($"WoW2 Files refused the call: {error.Message}", 502),
    };
}

/// <summary>Fills the file id of each image and release artifact stored before Files from the files the migration moved:
/// every Catalogue file in Files, matched by SHA-256 (Files t9 #5 step 6). Runs at boot as Catalogue itself; a file the
/// migration has not moved yet keeps reading from the local blob folder or SQLite.</summary>
public sealed class FilesMediaBackfill(ICatalogueRepository repository, IDesignRepository designs, IFileStoreBroker files, ILogger<FilesMediaBackfill> logger)
{
    public async Task RunAsync(CancellationToken ct)
    {
        var waiting = (await repository.GetMediaWithoutFileAsync(ct)).Select(record => record.Sha256).ToHashSet(StringComparer.Ordinal);
        var artifacts = new Dictionary<string, string>(await designs.AssetsWithoutFileAsync(ct), StringComparer.Ordinal);
        if (waiting.Count == 0 && artifacts.Count == 0) return;

        var filled = 0;
        string? cursor = null;
        do
        {
            var page = await files.ListAsync(new FileQuery { AppId = CatalogueFiles.AppId, Cursor = cursor, Limit = FilesConstants.MaxPageSize }, ct);
            if (page is Result<FilePage>.Failure failure)
            {
                logger.LogWarning("Catalogue's file ids stay unfilled: WoW2 Files answered {Type}: {Message}", failure.Error.Type, failure.Error.Message);
                return;
            }
            var (items, next) = page is Result<FilePage>.Success success ? (success.Value.Items, success.Value.NextCursor) : ([], null);
            foreach (var file in items)
            {
                if (waiting.Remove(file.Sha256))
                {
                    await repository.SetMediaFileAsync(file.Sha256, file.Id.ToString(), ct);
                    filled++;
                }
                if (artifacts.Remove(file.Sha256, out var contentType))
                {
                    await designs.SaveReleaseFileAsync(new CatalogueReleaseFileRecord { Sha256 = file.Sha256, ContentType = contentType, FileId = file.Id.ToString() }, ct);
                    filled++;
                }
            }
            cursor = next;
        }
        while (cursor is not null && (waiting.Count > 0 || artifacts.Count > 0));

        if (filled > 0 || waiting.Count > 0 || artifacts.Count > 0)
            logger.LogInformation("Catalogue filled {Filled} file ids from WoW2 Files; {Waiting} images still read from the local blob folder, {Artifacts} release artifacts from SQLite.", filled, waiting.Count, artifacts.Count);
    }
}
