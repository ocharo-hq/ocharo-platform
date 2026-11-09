namespace OcharoCatalogue.Domain.Catalogue.Entities;

/// <summary>Single-row settings document.</summary>
public sealed class SettingsRecord
{
    public string Id { get; set; } = "";
    public int Revision { get; set; }
    public string Json { get; set; } = "";
}
