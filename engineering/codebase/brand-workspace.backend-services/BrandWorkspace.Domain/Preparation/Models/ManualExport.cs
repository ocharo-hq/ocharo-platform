namespace BrandWorkspace.Domain.Preparation.Models;

/// <summary>A prepared manual package; creation is neither download nor publication confirmation.</summary>
public sealed record ManualExport(Guid Id, Guid DraftId, int Revision, DateTimeOffset CreatedAtUtc, string DownloadUrl)
{
    public string PackageKind => "manual-content-package";
    public bool MediaIncluded => true;
    public string PublicationStatus => "unverified";
}
