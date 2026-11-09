namespace BrandWorkspace.Domain.Commerce.Models;

/// <summary>Represents one rendered page of a catalogue edition and its artifacts.</summary>
public sealed record CatalogueEditionPage(string Id, string Title, double WidthMm, double HeightMm,
    IReadOnlyList<string> ProductIds, string PngSha256, string PdfSha256);
