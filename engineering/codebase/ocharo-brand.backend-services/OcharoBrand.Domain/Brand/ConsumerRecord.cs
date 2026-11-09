namespace OcharoBrand.Domain.Brand;

/// <summary>Which app last read the public brand, and which release it saw.</summary>
public sealed class ConsumerRecord
{
    public string App { get; set; } = "";
    public DateTimeOffset LastSeenAt { get; set; }
    public int LastRelease { get; set; }
    public string LastPath { get; set; } = "";
}
