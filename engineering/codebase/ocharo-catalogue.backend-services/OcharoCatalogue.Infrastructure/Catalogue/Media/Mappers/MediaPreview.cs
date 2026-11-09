using OcharoCatalogue.Domain.Catalogue.Entities;

namespace OcharoCatalogue.Infrastructure.Catalogue.Media.Mappers;

/// <summary>Maps a stored image to whether it has a web preview.</summary>
public static class MediaPreview
{
    /// <summary>Tells whether <paramref name="record"/> has a preview: a local file once its preview was written, a
    /// file in
    /// WoW2 Files always, as a variant.</summary>
    /// <param name="record">The stored image.</param>
    public static bool Exists(MediaBlobRecord record)
    {
        return record.PreviewPath is not null || record.FileId is not null;
    }
}
