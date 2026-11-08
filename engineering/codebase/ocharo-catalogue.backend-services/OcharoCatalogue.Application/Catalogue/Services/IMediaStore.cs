using OcharoCatalogue.Domain.Catalogue.Entities;

namespace OcharoCatalogue.Application.Catalogue.Services;

/// <summary>Content-addressed image storage with a web preview per image.</summary>
public interface IMediaStore
{
    /// <summary>Stores bytes once per SHA-256 (idempotent) and returns the index row.</summary>
    Task<MediaBlobRecord> StoreAsync(byte[] bytes, CancellationToken ct);

    /// <summary>Opens a stored file: a Files link to redirect to, or local bytes; null when unknown.</summary>
    Task<MediaContent?> OpenAsync(string sha256, bool preview, CancellationToken ct);

    /// <summary>Tells whether a stored file's bytes are there and still match their SHA-256.</summary>
    Task<MediaCheck> CheckAsync(string sha256, CancellationToken ct);
}

/// <summary>Where one stored file's bytes come from: a short-lived WoW2 Files link to redirect to
/// (<paramref name="Location"/>), or a stream from the local blob folder (<paramref name="Content"/>).</summary>
/// <param name="Record">The stored file.</param>
/// <param name="ContentType">The type the bytes carry: the original's, or WebP for a preview.</param>
/// <param name="Location">The Files link, valid for <see cref="LinkLifetime"/>; none for local bytes.</param>
/// <param name="Content">The local bytes; none for a Files link.</param>
public sealed record MediaContent(MediaBlobRecord Record, string ContentType, Uri? Location, Stream? Content)
{
    /// <summary>How long a Files link stays valid; a cached redirect must expire before it.</summary>
    public static readonly TimeSpan LinkLifetime = TimeSpan.FromMinutes(15);
}

/// <summary>What a stored file's check found.</summary>
public enum MediaCheck
{
    /// <summary>The bytes are there and match their SHA-256.</summary>
    Ok,

    /// <summary>No such file, or its bytes are gone.</summary>
    Missing,

    /// <summary>The bytes are there but no longer match.</summary>
    Corrupt,

    /// <summary>The store could not be read just now.</summary>
    Unavailable,
}

/// <summary>Whether a stored image has a web preview.</summary>
public static class MediaPreview
{
    /// <summary>A local file has one once its preview was written; a file in WoW2 Files always has one, as a variant.</summary>
    public static bool Exists(MediaBlobRecord record) => record.PreviewPath is not null || record.FileId is not null;
}
