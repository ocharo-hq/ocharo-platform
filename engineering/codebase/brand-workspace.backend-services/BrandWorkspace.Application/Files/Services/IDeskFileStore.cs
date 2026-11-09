using BrandWorkspace.Application.Files.Constants;
using BrandWorkspace.Application.Files.Models;

namespace BrandWorkspace.Application.Files.Services;

/// <summary>Desk's media in WoW2 Files. A run with <c>Files:BaseUrl</c> unset has none (<see cref="NoDeskFiles"/>), and
/// stores and reads bytes in PostgreSQL as before.</summary>
/// <remarks>
/// A person's request calls Files with their exchanged token, anything else (the import CLI, the boot backfill,
/// anonymous public routes) with Desk's own client-credentials token; the Files client picks by the signed-in session.
/// A Files failure surfaces as a <c>DeskErrors</c> catalog error: invalid 400, not found 404, unavailable 503, upstream
/// 502.
/// </remarks>
public interface IDeskFileStore
{
    /// <summary>Tells whether this run stores media in WoW2 Files.</summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Uploads bytes once per reference as app <c>desk</c>, private, with the reference as idempotency key, and
    /// attaches the reference that keeps them alive.
    /// </summary>
    Task<StoredFile> StoreAsync(
        byte[] bytes,
        string name,
        string contentType,
        DeskFileReference reference,
        CancellationToken token);

    /// <summary>Attaches one more record to a file already in Files; attaching twice is a no-op.</summary>
    Task AttachAsync(string fileId, DeskFileReference reference, CancellationToken token);

    /// <summary>
    /// Issues a link valid for <see cref="DeskFiles.LinkLifetime"/>: a variant at least <paramref name="width"/> wide
    /// when asked, saved under the file's name when <paramref name="download"/>.
    /// </summary>
    Task<Uri> LinkAsync(string fileId, int? width, bool download, CancellationToken token);

    /// <summary>Reads a file's bytes for server-side work, checked against the SHA-256 Desk recorded.</summary>
    Task<byte[]> ReadAsync(string fileId, string sha256, CancellationToken token);
}
