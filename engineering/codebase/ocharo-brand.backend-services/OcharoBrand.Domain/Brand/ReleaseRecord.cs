namespace OcharoBrand.Domain.Brand;

public sealed class ReleaseRecord
{
    public int Version { get; set; }
    public DateTimeOffset PublishedAt { get; set; }
    public string Json { get; set; } = "";
}
