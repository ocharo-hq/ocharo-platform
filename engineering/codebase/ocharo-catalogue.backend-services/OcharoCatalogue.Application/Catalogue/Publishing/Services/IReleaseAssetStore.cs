using OcharoCatalogue.Application.Catalogue.Media.Models;
using OcharoCatalogue.Application.Catalogue.Publishing.Models;

namespace OcharoCatalogue.Application.Catalogue.Publishing.Services;

/// <summary>Defines where a release's frozen PDFs and page PNGs live: SQLite rows in a run without WoW2 Files, WoW2
/// Files
/// otherwise, behind the public asset route (planning/files-adoption.md #26).</summary>
public interface IReleaseAssetStore
{
    /// <summary>Stores a prepared release's artifacts once per SHA-256; the result names what SQLite keeps and what
    /// Files
    /// holds, for the release's own transaction to record.</summary>
    /// <param name="artifacts">The release's artifacts.</param>
    /// <param name="ct">The token that cancels the write.</param>
    Task<ReleaseAssetStorage> StoreAsync(IReadOnlyList<ReleaseArtifact> artifacts, CancellationToken ct);

    /// <summary>Links an artifact held in Files, saved under its name and valid for <see
    /// cref="MediaContent.LinkLifetime"/>;
    /// null while SQLite holds its bytes alone.</summary>
    /// <param name="sha256">The artifact's SHA-256.</param>
    /// <param name="ct">The token that cancels the read.</param>
    Task<Uri?> LinkAsync(string sha256, CancellationToken ct);
}
