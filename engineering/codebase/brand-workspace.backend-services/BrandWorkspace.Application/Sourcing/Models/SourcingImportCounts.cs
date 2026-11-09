namespace BrandWorkspace.Application.Sourcing.Models;

/// <summary>Created, existing and failed totals for one record kind.</summary>
public sealed class SourcingImportCounts
{
    public int Created { get; set; }
    public int Existing { get; set; }
    public int Failed { get; set; }
    public long Bytes { get; set; }
}
