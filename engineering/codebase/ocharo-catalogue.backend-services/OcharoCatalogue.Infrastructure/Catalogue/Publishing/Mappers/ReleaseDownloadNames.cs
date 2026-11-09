using OcharoCatalogue.Application.Catalogue.Publishing.Models;

namespace OcharoCatalogue.Infrastructure.Catalogue.Publishing.Mappers;

/// <summary>Maps a release artifact to the file name the public reader offers.</summary>
public static class ReleaseDownloadNames
{
    /// <summary>
    /// Gets the download name of <paramref name="asset"/>: the whole edition by language, a page by its number.
    /// </summary>
    /// <param name="release">The prepared release.</param>
    /// <param name="asset">The artifact's name, such as <c>catalogue.pdf</c>.</param>
    public static string For(CataloguePreparedRelease release, string asset)
    {
        if (asset == release.PdfAsset)
        {
            return $"ocharo-{release.Slug}-{release.Locale}.pdf";
        }

        var number = release.Pages.ToList().FindIndex(page => page.PdfAsset == asset || page.PngAsset == asset) + 1;
        return $"ocharo-{release.Slug}-{number}{Path.GetExtension(asset)}";
    }
}
