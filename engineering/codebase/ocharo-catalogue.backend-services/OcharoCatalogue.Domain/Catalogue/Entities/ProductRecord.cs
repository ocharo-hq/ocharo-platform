namespace OcharoCatalogue.Domain.Catalogue.Entities;

/// <summary>Current product row: indexed scalars plus the serialized <c>ProductDocument</c>.</summary>
public sealed class ProductRecord
{
    public string Id { get; set; } = "";
    public string GarmentType { get; set; } = "";
    public string Status { get; set; } = "";
    public int SortOrder { get; set; }
    public int Revision { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string Json { get; set; } = "";
}
