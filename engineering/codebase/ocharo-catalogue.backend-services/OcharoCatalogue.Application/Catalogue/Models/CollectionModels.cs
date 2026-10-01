using OcharoCatalogue.Domain.Catalogue.Models;

namespace OcharoCatalogue.Application.Catalogue.Models;

/// <summary>A collection with its revision stamp.</summary>
public sealed record CollectionDetail(CollectionDocument Document, int Revision, DateTimeOffset UpdatedAt);

/// <summary>Create/update input for a collection.</summary>
public sealed record CollectionInput(
    int? ExpectedRevision,
    string? Id,
    CollectionNames Names,
    string? Description,
    IReadOnlyList<string>? ProductIds,
    bool IsPublished,
    int SortOrder);
