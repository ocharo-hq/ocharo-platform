namespace OcharoCatalogue.Domain.Catalogue.Entities;

/// <summary>An immutable snapshot of one saved product revision.</summary>
public sealed class ProductRevisionRecord
{
    public Guid Id { get; set; }
    public string ProductId { get; set; } = "";
    public int Revision { get; set; }
    public DateTimeOffset SavedAt { get; set; }
    public string Summary { get; set; } = "";
    public string Json { get; set; } = "";
}
