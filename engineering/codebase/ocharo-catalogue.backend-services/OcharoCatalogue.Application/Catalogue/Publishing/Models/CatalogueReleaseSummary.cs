namespace OcharoCatalogue.Application.Catalogue.Publishing.Models;

/// <summary>Represents a prepared release in a design's release list.</summary>
public sealed record CatalogueReleaseSummary(
    Guid Id,
    Guid DesignId,
    int DesignRevision,
    string Slug,
    string Locale,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? PublishedAtUtc,
    DateTimeOffset? WithdrawnAtUtc);
