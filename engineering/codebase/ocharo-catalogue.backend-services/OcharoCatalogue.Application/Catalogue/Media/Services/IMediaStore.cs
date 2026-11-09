using OcharoCatalogue.Application.Catalogue.Media.Enums;
using OcharoCatalogue.Application.Catalogue.Media.Models;
using OcharoCatalogue.Domain.Catalogue.Entities;

namespace OcharoCatalogue.Application.Catalogue.Media.Services;

/// <summary>Defines content-addressed image storage with a web preview per image.</summary>
public interface IMediaStore
{
    /// <summary>Stores bytes once per SHA-256 (idempotent) and returns the index row.</summary>
    /// <param name="bytes">The uploaded bytes.</param>
    /// <param name="ct">The token that cancels the write.</param>
    Task<MediaBlobRecord> StoreAsync(byte[] bytes, CancellationToken ct);

    /// <summary>Opens a stored file: a Files link to redirect to, or local bytes; null when unknown.</summary>
    /// <param name="sha256">The file's SHA-256.</param>
    /// <param name="preview">Whether the web preview is wanted rather than the original.</param>
    /// <param name="ct">The token that cancels the read.</param>
    Task<MediaContent?> OpenAsync(
        string sha256,
        bool preview,
        CancellationToken ct);

    /// <summary>Tells whether a stored file's bytes are there and still match their SHA-256.</summary>
    /// <param name="sha256">The file's SHA-256.</param>
    /// <param name="ct">The token that cancels the read.</param>
    Task<MediaCheck> CheckAsync(string sha256, CancellationToken ct);
}
