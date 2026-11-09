using OcharoCatalogue.Domain.Catalogue.Models;

namespace OcharoCatalogue.Application.Catalogue.Collections.Models;

/// <summary>Represents a collection with its revision stamp.</summary>
public sealed record CollectionDetail(
    CollectionDocument Document,
    int Revision,
    DateTimeOffset UpdatedAt);
