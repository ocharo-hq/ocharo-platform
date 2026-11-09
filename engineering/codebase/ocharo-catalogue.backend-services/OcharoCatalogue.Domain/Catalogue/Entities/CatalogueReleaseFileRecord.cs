namespace OcharoCatalogue.Domain.Catalogue.Entities;

/// <summary>A release artifact held in WoW2 Files, by SHA-256: prepared there, or moved there and filled at boot.</summary>
public sealed class CatalogueReleaseFileRecord
{
    public string Sha256 { get; set; } = "";
    public string ContentType { get; set; } = "";
    public string FileId { get; set; } = "";
}
