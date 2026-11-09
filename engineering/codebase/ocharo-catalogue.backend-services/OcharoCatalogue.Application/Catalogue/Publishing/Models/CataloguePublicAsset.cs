namespace OcharoCatalogue.Application.Catalogue.Publishing.Models;

/// <summary>Represents a published artifact to serve: a short-lived WoW2 Files link to redirect to
/// (<paramref name="Location"/>), or the bytes SQLite holds (<paramref name="Bytes"/>).</summary>
public sealed record CataloguePublicAsset(
    string FileName,
    string Sha256,
    string ContentType,
    byte[]? Bytes,
    Uri? Location = null);
