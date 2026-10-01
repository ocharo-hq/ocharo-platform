namespace BrandWorkspace.Domain.DesignDocuments.Models;

/// <summary>Lists saved documents without transferring editor snapshots.</summary>
public sealed record DesignDocumentSummary(
    Guid Id,
    Guid OrganizationId,
    string Kind,
    string Title,
    int Revision,
    bool Archived,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc)
{
    public string? Locale { get; init; }
    public CatalogueSummary? Catalogue { get; init; }
}
public sealed record CatalogueSummary(string Slug, string Description, bool IsActive, CatalogueOutputs Outputs, CatalogueWebSummary Web, int PdfPageCount);
public sealed record CatalogueOutputs(bool Web, bool Pdf);
public sealed record CatalogueWebSummary(string Theme, int PageCount);
