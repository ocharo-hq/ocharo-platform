namespace BrandWorkspace.Api.RuntimeConfig.Settings;

/// <summary>Holds the <c>OcharoApps</c> section: the public addresses of the Ocharo apps and sites Desk links to.</summary>
/// <remarks>Every address is optional; Desk's app menu keeps its build-time or local address for one left unset.</remarks>
public sealed record OcharoAppsSettings
{
    public const string SectionName = "OcharoApps";

    public string? Desk { get; init; }
    public string? Catalogue { get; init; }
    public string? Studio { get; init; }
    public string? Marketing { get; init; }
    public string? Editorial { get; init; }
    public string? Brand { get; init; }
    public string? Atlas { get; init; }
    public string? Wear { get; init; }
    public string? Blog { get; init; }
}
