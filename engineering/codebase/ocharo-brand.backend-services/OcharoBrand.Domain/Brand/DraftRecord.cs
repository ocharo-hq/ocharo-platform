namespace OcharoBrand.Domain.Brand;

/// <summary>Single-row draft document (identity + current logos), revision-checked.</summary>
public sealed class DraftRecord
{
    public string Id { get; set; } = "";
    public int Revision { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string Json { get; set; } = "";
}
