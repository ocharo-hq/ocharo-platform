namespace BrandWorkspace.Application.Events.Models;

/// <summary>Counts what an import created or found already present, and the events it could not store.</summary>
public sealed class EventImportReport(bool dryRun, string? source)
{
    public bool DryRun { get; } = dryRun;
    public string? Source { get; } = source;
    public int Created { get; set; }
    public int Existing { get; set; }
    public List<EventImportFailure> Failures { get; } = [];
}
