namespace OcharoCatalogue.Domain.Catalogue.Entities;

/// <summary>Mutable pointer to one prepared release; publishing never rewrites released bytes.</summary>
public sealed class CataloguePublicationRecord
{
    public string Key { get; set; } = "";
    public Guid DesignId { get; set; }
    public Guid ReleaseId { get; set; }
}
