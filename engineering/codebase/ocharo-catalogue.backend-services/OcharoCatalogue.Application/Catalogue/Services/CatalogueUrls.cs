namespace OcharoCatalogue.Application.Catalogue.Services;

/// <summary>Stable, content-addressed media routes shared by every view.</summary>
public static class CatalogueUrls
{
    public static string Media(string sha256) => $"/api/catalogue/media/{sha256}";

    public static string Preview(string sha256) => $"/api/catalogue/media/{sha256}/preview";
}
