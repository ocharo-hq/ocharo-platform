using OcharoCatalogue.Application.Catalogue.Publishing.Models;
using OcharoCatalogue.Domain.Catalogue.Entities;

namespace OcharoCatalogue.Infrastructure.Catalogue.Publishing.Mappers;

/// <summary>Maps a stored release to its summary in a design's release list.</summary>
public static class ReleaseSummaries
{
    /// <summary>Maps <paramref name="record"/> to its summary.</summary>
    /// <param name="record">The release row.</param>
    public static CatalogueReleaseSummary ToSummary(CatalogueReleaseRecord record)
    {
        return new CatalogueReleaseSummary(
            record.Id,
            record.DesignId,
            record.DesignRevision,
            record.Slug,
            record.Locale,
            record.CreatedAtUtc,
            record.PublishedAtUtc,
            record.WithdrawnAtUtc);
    }
}
