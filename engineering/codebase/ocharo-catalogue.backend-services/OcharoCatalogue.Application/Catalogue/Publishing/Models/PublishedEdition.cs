using System.Text.Json;

namespace OcharoCatalogue.Application.Catalogue.Publishing.Models;

/// <summary>Represents a published edition as the public reader shows it.</summary>
public sealed record PublishedEdition(
    Guid Id,
    Guid DesignId,
    int DesignRevision,
    string Slug,
    string Title,
    string Description,
    string Locale,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? PublishedAtUtc,
    IReadOnlyList<string> AvailableLocales,
    JsonElement? Web,
    IReadOnlyList<PublicProduct> Products,
    PublishedEditionPdf? Pdf);
