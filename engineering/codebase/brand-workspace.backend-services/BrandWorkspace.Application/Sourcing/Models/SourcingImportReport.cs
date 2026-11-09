namespace BrandWorkspace.Application.Sourcing.Models;

/// <summary>Counts what an import created, found already present, or could not store.</summary>
public sealed class SourcingImportReport(bool dryRun, string? source)
{
    public bool DryRun { get; } = dryRun;
    public string? Source { get; } = source;
    public SourcingImportCounts Suppliers { get; } = new();
    public SourcingImportCounts People { get; } = new();
    public SourcingImportCounts Routes { get; } = new();
    public SourcingImportCounts Media { get; } = new();
    public List<SourcingImportFailure> Failures { get; } = [];
}
