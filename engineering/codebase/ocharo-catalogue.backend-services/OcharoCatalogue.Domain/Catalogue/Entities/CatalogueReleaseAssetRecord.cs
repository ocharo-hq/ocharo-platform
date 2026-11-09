namespace OcharoCatalogue.Domain.Catalogue.Entities;

/// <summary>A release artifact whose bytes SQLite holds: every artifact a run without WoW2 Files prepared, and those
/// prepared before Files until the migration moves them.</summary>
public sealed class CatalogueReleaseAssetRecord
{
    public string Sha256 { get; set; } = "";
    public string ContentType { get; set; } = "";
    public byte[] Bytes { get; set; } = [];
}
