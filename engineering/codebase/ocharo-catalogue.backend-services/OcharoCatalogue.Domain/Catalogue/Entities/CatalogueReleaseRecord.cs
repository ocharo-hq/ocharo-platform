namespace OcharoCatalogue.Domain.Catalogue.Entities;

/// <summary>The payload and source fingerprint never change after preparation.</summary>
public sealed class CatalogueReleaseRecord
{
    public Guid Id { get; set; }
    public Guid DesignId { get; set; }
    public int DesignRevision { get; set; }
    public string Slug { get; set; } = "";
    public string Locale { get; set; } = "";
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? PublishedAtUtc { get; set; }
    public DateTimeOffset? WithdrawnAtUtc { get; set; }
    public string Fingerprint { get; set; } = "";
    public string ProductIdsJson { get; set; } = "[]";
    public string Json { get; set; } = "";
}
