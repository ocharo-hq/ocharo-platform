using OcharoCatalogue.Domain.Catalogue.Models;

namespace OcharoCatalogue.Application.Catalogue.Collections.Models;

/// <summary>Represents the editable fields of a collection.</summary>
public sealed record CollectionInput(
    int? ExpectedRevision,
    string? Id,
    CollectionNames Names,
    string? Description,
    IReadOnlyList<string>? ProductIds,
    bool IsPublished,
    int SortOrder);
