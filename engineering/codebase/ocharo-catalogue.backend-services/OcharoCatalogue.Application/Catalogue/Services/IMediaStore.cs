using OcharoCatalogue.Domain.Catalogue.Entities;

namespace OcharoCatalogue.Application.Catalogue.Services;

/// <summary>Content-addressed image storage with a web preview per image.</summary>
public interface IMediaStore
{
    /// <summary>Stores bytes once per SHA-256 (idempotent) and returns the index row.</summary>
    Task<MediaBlobRecord> StoreAsync(byte[] bytes, CancellationToken ct);

    /// <summary>Opens the original bytes, or null when unknown.</summary>
    Task<(MediaBlobRecord Record, Stream Content)?> OpenAsync(string sha256, bool preview, CancellationToken ct);
}
