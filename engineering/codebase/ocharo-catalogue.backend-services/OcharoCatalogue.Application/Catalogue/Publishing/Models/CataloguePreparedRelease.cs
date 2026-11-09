using System.Text.Json;

namespace OcharoCatalogue.Application.Catalogue.Publishing.Models;

/// <summary>Represents an immutable release prepared from a saved design and reviewed product revisions.</summary>
public sealed record CataloguePreparedRelease(
    Guid Id,
    Guid DesignId,
    int DesignRevision,
    string Slug,
    string Title,
    string Description,
    string Locale,
    DateTimeOffset CreatedAtUtc,
    JsonElement? Web,
    IReadOnlyList<PublicProduct> Products,
    IReadOnlyList<CatalogueReleasePage> Pages,
    string? PdfAsset,
    IReadOnlyDictionary<string, string> Assets);
