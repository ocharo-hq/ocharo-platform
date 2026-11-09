namespace OcharoBrand.Domain.Brand;

public sealed class LogoVersionRecord
{
    public string Id { get; set; } = "";
    public string Slot { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public DateTimeOffset UploadedAt { get; set; }
    public string Json { get; set; } = "";
}
