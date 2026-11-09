namespace OcharoCatalogue.Domain.Catalogue.Entities;

public sealed class CatalogueDesignRevisionRecord
{
    public Guid Id { get; set; }
    public Guid DesignId { get; set; }
    public int Revision { get; set; }
    public string Title { get; set; } = "";
    public DateTimeOffset SavedAtUtc { get; set; }
    public string Json { get; set; } = "";
}
