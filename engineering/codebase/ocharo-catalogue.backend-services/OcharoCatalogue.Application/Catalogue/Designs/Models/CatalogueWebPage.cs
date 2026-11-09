namespace OcharoCatalogue.Application.Catalogue.Designs.Models;

/// <summary>Represents one page of a design's web output.</summary>
public sealed record CatalogueWebPage(
    string Id,
    string Title,
    string Layout,
    bool IsActive,
    IReadOnlyList<string> ProductIds);
