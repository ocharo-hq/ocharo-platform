namespace OcharoCatalogue.Application.Catalogue.Designs.Models;

/// <summary>Represents the catalogue metadata a gallery card shows.</summary>
public sealed record CatalogueDesignSummaryMetadata(
    string Slug,
    string Description,
    bool IsActive,
    CatalogueOutputs Outputs,
    CatalogueWebSummary Web,
    int PdfPageCount);
