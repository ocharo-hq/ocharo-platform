namespace OcharoCatalogue.Application.Catalogue.Publishing.Models;

/// <summary>Represents what a release would hold if prepared now, for the designer to render against.</summary>
/// <param name="DesignRevision">The saved design revision.</param>
/// <param name="Fingerprint">The fingerprint of the products and settings the release reads.</param>
/// <param name="PdfPageIds">The PDF pages the release would render.</param>
/// <param name="ExcludedProductIds">The referenced products not confirmed for the design's language.</param>
public sealed record ReleasePreparation(
    int DesignRevision,
    string Fingerprint,
    IReadOnlyList<string> PdfPageIds,
    IReadOnlyList<string> ExcludedProductIds);
