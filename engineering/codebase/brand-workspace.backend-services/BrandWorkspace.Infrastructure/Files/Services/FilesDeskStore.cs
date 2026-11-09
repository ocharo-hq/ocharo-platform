using BrandWorkspace.Application.Foundation.Errors;
using System.Security.Cryptography;
using BrandWorkspace.Application.Files.Constants;
using BrandWorkspace.Application.Files.Models;
using BrandWorkspace.Application.Files.Services;
using BrandWorkspace.Application.Files.Repositories;
using BrandWorkspace.Application.Preparation.Models;
using Microsoft.Extensions.Logging;
using WoW.Two.Sdk.Beta.Be.Files;
using WoW.Two.Sdk.Beta.Be.Files.References;
using WoW.Two.Sdk.Beta.Be.Files.Store;
using WoW.Two.Sdk.Beta.Be.Files.Uploads;
using WoW.Two.Sdk.Beta.Be.Files.Urls;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace BrandWorkspace.Infrastructure.Files.Services;

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
            throw DeskErrors.Conflict("WoW2 Files holds other bytes under this reference.").ToException();
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
            throw DeskErrors.Conflict("A stored file failed its integrity check.").ToException();
        return bytes;
    }

    /// <summary>Returns a Files call's value, or refuses the Desk request with the status its failure maps to.</summary>
    internal static T Unwrap<T>(Result<T> result) where T : notnull => result.Match(value => value, error => throw Refusal(error));

    private static void Unwrap(Result result) => result.Match(() => true, error => throw Refusal(error));

    private static FileId Parse(string fileId) => FileId.TryParse(fileId, out var id)
        ? id
        : throw new InvalidOperationException("A stored file id is malformed.");

    private static AppException Refusal(AppError error) => error.Type switch
    {
        AppErrorType.Validation => DeskErrors.Invalid(error.Message).ToException(),
        AppErrorType.NotFound or AppErrorType.FileNotFound => DeskErrors.NotFound("The file is gone from WoW2 Files.").ToException(),
        AppErrorType.ExternalUnavailable => DeskErrors.Unavailable("WoW2 Files is unavailable; try again shortly.").ToException(),
        _ => DeskErrors.Upstream($"WoW2 Files refused the call: {error.Message}").ToException(),
    };
}
