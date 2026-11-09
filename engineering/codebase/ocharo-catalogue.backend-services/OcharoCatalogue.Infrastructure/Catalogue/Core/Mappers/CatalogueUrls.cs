namespace OcharoCatalogue.Infrastructure.Catalogue.Core.Mappers;

/// <summary>Maps a stored file's SHA-256 to its stable, content-addressed media routes, shared by every view.</summary>
public static class CatalogueUrls
{
    /// <summary>Maps <paramref name="sha256"/> to the original's route.</summary>
    /// <param name="sha256">The stored file's SHA-256.</param>
    public static string Media(string sha256)
    {
        return $"/api/catalogue/media/{sha256}";
    }

    /// <summary>Maps <paramref name="sha256"/> to the web preview's route.</summary>
    /// <param name="sha256">The stored file's SHA-256.</param>
    public static string Preview(string sha256)
    {
        return $"/api/catalogue/media/{sha256}/preview";
    }
}
