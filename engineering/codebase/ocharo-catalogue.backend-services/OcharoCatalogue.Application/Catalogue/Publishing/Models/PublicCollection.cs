using OcharoCatalogue.Domain.Catalogue.Models;

namespace OcharoCatalogue.Application.Catalogue.Publishing.Models;

/// <summary>Represents a published collection in the public feed.</summary>
public sealed record PublicCollection(
    string Id,
    string Name,
    CollectionNames Names,
    string? Description,
    IReadOnlyList<string> ProductIds);
