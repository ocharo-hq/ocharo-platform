namespace BrandWorkspace.Application.Files;

/// <summary>The names Desk's files carry in WoW2 Files: the same app and references the migration manifest
/// (<c>engineering/deployment/files-migration.yml</c>, wave 3) gives the files it moves, so imports and new uploads share
/// one policy and the boot backfill finds both (planning/files-adoption.md #13, #20, #21).</summary>
public static class DeskFiles
{
    /// <summary>The app every Desk file belongs to: Desk's Identity client id.</summary>
    public const string AppId = "desk";

    /// <summary>The record type of an operator's preparation image; its record id is <c>{organization_id}/{id}</c>.</summary>
    public const string PreparationMedia = "desk.preparation-media";

    /// <summary>The record type of an approved revision's export ZIP; its record id is <c>{organization_id}/{id}</c>.</summary>
    public const string PreparationExport = "desk.preparation-export";

    /// <summary>The record type of a brand's commerce image or edition artifact; its record id is <c>{brand_id}/{sha256}</c>.</summary>
    public const string CommerceMedia = "desk.commerce-media";

    /// <summary>The record type of a supplier image, PDF or video; its record id is <c>{organization_id}/{sha256}</c>.</summary>
    public const string SourcingMedia = "desk.sourcing-media";

    /// <summary>How long a Files link stays valid.</summary>
    public static readonly TimeSpan LinkLifetime = TimeSpan.FromMinutes(15);

    /// <summary>The width of a sourcing thumbnail, now a Files variant (planning/files-adoption.md #10).</summary>
    public const int SourcingThumbnailWidth = 480;

    /// <summary>The name a file carries in Files: its hash and the extension Files checks against its bytes.</summary>
    public static string Name(string sha256, string contentType) => $"{sha256[..16]}.{Extension(contentType)}";

    /// <summary>The file extension of a content type Desk stores.</summary>
    public static string Extension(string contentType) => contentType switch
    {
        "image/png" => "png",
        "image/jpeg" => "jpg",
        "image/webp" => "webp",
        "application/pdf" => "pdf",
        "video/mp4" => "mp4",
        "application/zip" => "zip",
        _ => "bin",
    };
}

/// <summary>Names the Desk record that keeps a file alive: <c>{RecordType}:{RecordId}</c> is also the upload's idempotency key
/// and the manifest's reference.</summary>
public sealed record DeskFileReference(string RecordType, string RecordId)
{
    public static DeskFileReference PreparationMedia(Guid organizationId, Guid mediaId) =>
        new(DeskFiles.PreparationMedia, $"{organizationId:D}/{mediaId:D}");

    public static DeskFileReference PreparationExport(Guid organizationId, Guid exportId) =>
        new(DeskFiles.PreparationExport, $"{organizationId:D}/{exportId:D}");

    public static DeskFileReference CommerceMedia(Guid brandId, string sha256) =>
        new(DeskFiles.CommerceMedia, $"{brandId:D}/{sha256}");

    public static DeskFileReference SourcingMedia(Guid organizationId, string sha256) =>
        new(DeskFiles.SourcingMedia, $"{organizationId:D}/{sha256}");

    public override string ToString() => $"{RecordType}:{RecordId}";
}

/// <summary>A file Desk holds in WoW2 Files.</summary>
public sealed record StoredFile(string Id, string Sha256, long Size);

/// <summary>Where one route's bytes come from: a short-lived Files link to redirect to, or bytes to answer with.</summary>
public sealed record StoredContent(string ContentType, Uri? Location, byte[]? Bytes);

/// <summary>Desk's media in WoW2 Files. A run with <c>Files:BaseUrl</c> unset has none (<see cref="NoDeskFiles"/>), and
/// stores and reads bytes in PostgreSQL as before.</summary>
/// <remarks>A person's request calls Files with their exchanged token, anything else (the import CLI, the boot backfill,
/// anonymous public routes) with Desk's own client-credentials token; the Files client picks by the signed-in session.
/// A Files failure surfaces as a <c>PreparationException</c>: <c>validation</c> 400, <c>not-found</c> 404,
/// <c>unavailable</c> 503, <c>upstream</c> 502.</remarks>
public interface IDeskFileStore
{
    /// <summary>Tells whether this run stores media in WoW2 Files.</summary>
    bool IsEnabled { get; }

    /// <summary>Uploads bytes once per reference as app <c>desk</c>, private, with the reference as idempotency key, and
    /// attaches the reference that keeps them alive.</summary>
    Task<StoredFile> StoreAsync(byte[] bytes, string name, string contentType, DeskFileReference reference, CancellationToken token);

    /// <summary>Attaches one more record to a file already in Files; attaching twice is a no-op.</summary>
    Task AttachAsync(string fileId, DeskFileReference reference, CancellationToken token);

    /// <summary>Issues a link valid for <see cref="DeskFiles.LinkLifetime"/>: a variant at least <paramref name="width"/>
    /// wide when asked, saved under the file's name when <paramref name="download"/>.</summary>
    Task<Uri> LinkAsync(string fileId, int? width, bool download, CancellationToken token);

    /// <summary>Reads a file's bytes for server-side work, checked against the SHA-256 Desk recorded.</summary>
    Task<byte[]> ReadAsync(string fileId, string sha256, CancellationToken token);
}

/// <summary>The store of a run without WoW2 Files: every byte stays in PostgreSQL.</summary>
public sealed class NoDeskFiles : IDeskFileStore
{
    public static readonly NoDeskFiles Instance = new();

    public bool IsEnabled => false;

    public Task<StoredFile> StoreAsync(byte[] bytes, string name, string contentType, DeskFileReference reference, CancellationToken token) =>
        throw Disabled();

    public Task AttachAsync(string fileId, DeskFileReference reference, CancellationToken token) => throw Disabled();

    public Task<Uri> LinkAsync(string fileId, int? width, bool download, CancellationToken token) => throw Disabled();

    public Task<byte[]> ReadAsync(string fileId, string sha256, CancellationToken token) => throw Disabled();

    private static InvalidOperationException Disabled() =>
        new("WoW2 Files is not configured (Files:BaseUrl), yet a stored row names a Files file.");
}

/// <summary>A row stored before Files that still reads its PostgreSQL bytes, and the reference its moved file carries.</summary>
public sealed record DeskFileWaiting(DeskFileReference Reference, string Sha256);

/// <summary>Finds the rows stored before Files and records the files the migration moved for them. Rows of the immutable
/// <c>preparation.media</c> and <c>preparation.exports</c> never change: their file id is copied into an insert-only record
/// keyed like the row, and every read repoints through it (migration 012).</summary>
public interface IDeskFileIdRepository
{
    Task<IReadOnlyList<DeskFileWaiting>> GetWaitingAsync(CancellationToken token);
    Task FillAsync(DeskFileWaiting row, string fileId, CancellationToken token);
}
