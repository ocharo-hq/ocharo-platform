namespace BrandWorkspace.Domain.DesignDocuments.Models;

/// <summary>Represents a catalogue design's settings as a document list shows them.</summary>
public sealed record CatalogueSummary(
    string Slug,
    string Description,
    bool IsActive,
    CatalogueOutputs Outputs,
    CatalogueWebSummary Web,
    int PdfPageCount);
