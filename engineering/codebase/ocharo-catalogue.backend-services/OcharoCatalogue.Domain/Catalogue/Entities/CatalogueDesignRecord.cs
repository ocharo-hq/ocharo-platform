namespace OcharoCatalogue.Domain.Catalogue.Entities;

/// <summary>Current named catalogue design; saves retain immutable revisions.</summary>
public sealed class CatalogueDesignRecord
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public string Slug { get; set; } = "";
    public string Locale { get; set; } = "";
    public int Revision { get; set; }
    public bool Archived { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public string Json { get; set; } = "";
}
