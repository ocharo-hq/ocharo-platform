using System.Security.Cryptography;
using BrandWorkspace.Application.Files;
using BrandWorkspace.Application.Preparation.Models;
using Microsoft.Extensions.Logging;
using WoW.Two.Sdk.Beta.Be.Files;
using WoW.Two.Sdk.Beta.Be.Files.References;
using WoW.Two.Sdk.Beta.Be.Files.Store;
using WoW.Two.Sdk.Beta.Be.Files.Uploads;
using WoW.Two.Sdk.Beta.Be.Files.Urls;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace BrandWorkspace.Infrastructure.Files;

/// <summary>Desk's media in WoW2 Files, through the be sdk's Files brokers (planning/files-adoption.md #13).</summary>
public sealed class FilesDeskStore(
    IFileUploadsBroker uploads,
    IFileReferencesBroker references,
    IFileUrlsBroker urls,
    IFileStoreBroker store) : IDeskFileStore
{
    public bool IsEnabled => true;

    public async Task<StoredFile> StoreAsync(byte[] bytes, string name, string contentType, DeskFileReference reference, CancellationToken token)
    {
        var sha = Convert.ToHexStringLower(SHA256.HashData(bytes));
        FileModel file;
        await using (var content = new MemoryStream(bytes, false))
        {
            var spec = new UploadSpec
            {
                Name = name,
                ContentType = contentType,
                AppId = DeskFiles.AppId,
                Visibility = FileVisibility.Private,
                IdempotencyKey = reference.ToString(),
            };
            file = Unwrap(await uploads.UploadAsync(content, spec, token));
        }
        // An idempotency key answers with its first upload; other bytes under the same reference are never adopted.
        if (!string.Equals(file.Sha256, sha, StringComparison.Ordinal))
            throw new PreparationException("conflict", "WoW2 Files holds other bytes under this reference.");
        Unwrap(await references.AttachAsync(file.Id, new FileReference(reference.RecordType, reference.RecordId), token));
        return new StoredFile(file.Id.ToString(), file.Sha256, file.Size);
    }

    public async Task AttachAsync(string fileId, DeskFileReference reference, CancellationToken token) =>
        Unwrap(await references.AttachAsync(Parse(fileId), new FileReference(reference.RecordType, reference.RecordId), token));

    public async Task<Uri> LinkAsync(string fileId, int? width, bool download, CancellationToken token)
    {
        var spec = new UrlSpec
        {
            Lifetime = DeskFiles.LinkLifetime,
            Width = width,
            Disposition = download ? FileDisposition.Attachment : FileDisposition.Inline,
        };
        return Unwrap(await urls.IssueAsync(Parse(fileId), spec, token)).Url;
    }

    public async Task<byte[]> ReadAsync(string fileId, string sha256, CancellationToken token)
    {
        var content = Unwrap(await store.OpenReadAsync(Parse(fileId), null, token));
        await using var stream = content.Content;
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, token);
        var bytes = buffer.ToArray();
        if (!string.Equals(Convert.ToHexStringLower(SHA256.HashData(bytes)), sha256, StringComparison.Ordinal))
            throw new PreparationException("conflict", "A stored file failed its integrity check.");
        return bytes;
    }

    /// <summary>Returns a Files call's value, or refuses the Desk request with the status its failure maps to.</summary>
    internal static T Unwrap<T>(Result<T> result) where T : notnull => result.Match(value => value, error => throw Refusal(error));

    private static void Unwrap(Result result) => result.Match(() => true, error => throw Refusal(error));

    private static FileId Parse(string fileId) => FileId.TryParse(fileId, out var id)
        ? id
        : throw new InvalidOperationException("A stored file id is malformed.");

    private static PreparationException Refusal(AppError error) => error.Type switch
    {
        AppErrorType.Validation => new PreparationException("validation", error.Message),
        AppErrorType.NotFound or AppErrorType.FileNotFound => new PreparationException("not-found", "The file is gone from WoW2 Files."),
        AppErrorType.ExternalUnavailable => new PreparationException("unavailable", "WoW2 Files is unavailable; try again shortly."),
        _ => new PreparationException("upstream", $"WoW2 Files refused the call: {error.Message}"),
    };
}

/// <summary>Fills the file id of each row stored before Files from the files the migration moved (Files t9 #5 step 6): every
/// <c>desk</c> file in Files whose SHA-256 matches a waiting row and that carries that row's own reference, so a file
/// never crosses an organization, brand or record. Runs at boot as Desk itself; a row the migration has not moved yet
/// keeps reading its PostgreSQL bytes.</summary>
public sealed class DeskFilesBackfill(
    IDeskFileIdRepository rows,
    IFileStoreBroker files,
    IFileReferencesBroker references,
    ILogger<DeskFilesBackfill> logger)
{
    public async Task RunAsync(CancellationToken token)
    {
        var waiting = (await rows.GetWaitingAsync(token))
            .GroupBy(row => row.Sha256, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
        if (waiting.Count == 0) return;

        var filled = 0;
        string? cursor = null;
        do
        {
            var page = await files.ListAsync(new FileQuery { AppId = DeskFiles.AppId, Cursor = cursor, Limit = FilesConstants.MaxPageSize }, token);
            if (page is Result<FilePage>.Failure failure)
            {
                logger.LogWarning("Desk's file ids stay unfilled: WoW2 Files answered {Type}: {Message}", failure.Error.Type, failure.Error.Message);
                return;
            }
            var (items, next) = page is Result<FilePage>.Success success ? (success.Value.Items, success.Value.NextCursor) : ([], null);
            foreach (var file in items)
            {
                if (file.State != FileState.Ready || !waiting.TryGetValue(file.Sha256, out var candidates)) continue;
                var listed = await references.ListAsync(file.Id, token);
                if (listed is not Result<IReadOnlyList<FileReference>>.Success attached) continue;
                foreach (var row in candidates.Where(row => attached.Value.Contains(new FileReference(row.Reference.RecordType, row.Reference.RecordId))).ToList())
                {
                    await rows.FillAsync(row, file.Id.ToString(), token);
                    candidates.Remove(row);
                    filled++;
                }
                if (candidates.Count == 0) waiting.Remove(file.Sha256);
            }
            cursor = next;
        }
        while (cursor is not null && waiting.Count > 0);

        logger.LogInformation("Desk filled {Filled} file ids from WoW2 Files; {Waiting} rows still read their PostgreSQL bytes.",
            filled, waiting.Values.Sum(candidates => candidates.Count));
    }
}
