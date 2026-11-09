using OcharoCatalogue.Application.Catalogue.Collections.Models;
using OcharoCatalogue.Application.Catalogue.Core.Serializers;
using OcharoCatalogue.Domain.Catalogue.Entities;
using OcharoCatalogue.Domain.Catalogue.Models;

namespace OcharoCatalogue.Infrastructure.Catalogue.Collections.Mappers;

/// <summary>Maps a stored collection to its detail.</summary>
public static class CollectionDetails
{
    /// <summary>Maps <paramref name="record"/> to its detail.</summary>
    /// <param name="record">The collection row.</param>
    public static CollectionDetail ToDetail(CollectionRecord record)
    {
        return new CollectionDetail(
            CatalogueJson.Read<CollectionDocument>(record.Json),
            record.Revision,
            record.UpdatedAt);
    }
}
