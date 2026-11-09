namespace OcharoBrand.Domain.Brand;

public sealed class AppearanceRecord
{
    public string Id { get; set; } = "";
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public string Status { get; set; } = "";
    public string Json { get; set; } = "";
}
