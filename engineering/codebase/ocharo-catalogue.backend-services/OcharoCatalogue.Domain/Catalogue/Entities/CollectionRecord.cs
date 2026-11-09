namespace OcharoCatalogue.Domain.Catalogue.Entities;

/// <summary>Current collection row.</summary>
public sealed class CollectionRecord
{
    public string Id { get; set; } = "";
    public int SortOrder { get; set; }
    public int Revision { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string Json { get; set; } = "";
}
