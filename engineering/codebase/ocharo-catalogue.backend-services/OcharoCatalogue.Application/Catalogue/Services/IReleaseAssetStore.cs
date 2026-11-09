using OcharoCatalogue.Domain.Catalogue.Entities;

namespace OcharoCatalogue.Application.Catalogue.Services;

/// <summary>Where a release's frozen PDFs and page PNGs live: SQLite rows in a run without WoW2 Files, WoW2 Files
/// otherwise, behind the public asset route (planning/files-adoption.md #26).</summary>
public interface IReleaseAssetStore
{
    /// <summary>Stores a prepared release's artifacts once per SHA-256; the result names what SQLite keeps and what Files
    /// holds, for the release's own transaction to record.</summary>
    Task<ReleaseAssetStorage> StoreAsync(IReadOnlyList<ReleaseArtifact> artifacts, CancellationToken ct);

    /// <summary>Links an artifact held in Files, saved under its name and valid for <see cref="MediaContent.LinkLifetime"/>;
    /// null while SQLite holds its bytes alone.</summary>
    Task<Uri?> LinkAsync(string sha256, CancellationToken ct);
}

/// <summary>One artifact of a prepared release.</summary>
/// <param name="Sha256">The SHA-256 of its bytes, which addresses it.</param>
/// <param name="ContentType"><c>application/pdf</c> or <c>image/png</c>.</param>
/// <param name="FileName">The name a download saves it under; Files keeps the name of the first release with these bytes.</param>
/// <param name="Bytes">Its bytes.</param>
public sealed record ReleaseArtifact(string Sha256, string ContentType, string FileName, byte[] Bytes);

/// <summary>Where a release's artifacts went.</summary>
/// <param name="Rows">The rows whose bytes SQLite keeps.</param>
/// <param name="Files">The files WoW2 Files holds.</param>
public sealed record ReleaseAssetStorage(IReadOnlyList<CatalogueReleaseAssetRecord> Rows, IReadOnlyList<CatalogueReleaseFileRecord> Files);
