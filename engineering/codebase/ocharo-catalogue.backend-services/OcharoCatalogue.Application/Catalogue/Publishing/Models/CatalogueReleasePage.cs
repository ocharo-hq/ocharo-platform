namespace OcharoCatalogue.Application.Catalogue.Publishing.Models;

/// <summary>Represents one PDF page of a prepared release with its frozen artifacts.</summary>
public sealed record CatalogueReleasePage(
    string Id,
    string Title,
    double WidthMm,
    double HeightMm,
    IReadOnlyList<string> ProductIds,
    string PngAsset,
    string PdfAsset);
