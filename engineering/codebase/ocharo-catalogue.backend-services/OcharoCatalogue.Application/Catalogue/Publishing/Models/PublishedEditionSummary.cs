namespace OcharoCatalogue.Application.Catalogue.Publishing.Models;

/// <summary>Represents a published edition in the public list of one language.</summary>
public sealed record PublishedEditionSummary(
    Guid Id,
    string Slug,
    string Title,
    string Description,
    string Locale,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<string> AvailableLocales,
    bool HasWeb,
    bool HasPdf);
