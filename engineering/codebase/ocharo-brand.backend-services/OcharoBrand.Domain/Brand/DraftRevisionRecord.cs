namespace OcharoBrand.Domain.Brand;

/// <summary>Immutable draft snapshot per save.</summary>
public sealed class DraftRevisionRecord
{
    public Guid Id { get; set; }
    public int Revision { get; set; }
    public DateTimeOffset SavedAt { get; set; }
    public string Summary { get; set; } = "";
    public string Json { get; set; } = "";
}
