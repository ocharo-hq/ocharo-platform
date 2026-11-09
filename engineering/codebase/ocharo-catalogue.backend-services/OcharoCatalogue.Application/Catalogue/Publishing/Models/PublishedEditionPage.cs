namespace OcharoCatalogue.Application.Catalogue.Publishing.Models;

/// <summary>Represents one page of a published edition's PDF output.</summary>
public sealed record PublishedEditionPage(
    string Id,
    int Number,
    string Title,
    double WidthMm,
    double HeightMm,
    IReadOnlyList<string> ProductIds,
    string ImageUrl,
    string PdfUrl);
