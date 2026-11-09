namespace OcharoCatalogue.Application.Catalogue.Designs.Models;

/// <summary>Represents a catalogue design in the gallery.</summary>
public sealed record CatalogueDesignSummary(
    Guid Id,
    string Title,
    int Revision,
    bool Archived,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string Locale,
    CatalogueDesignSummaryMetadata Catalogue);
