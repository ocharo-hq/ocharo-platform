using OcharoCatalogue.Application.Catalogue.Publishing.Models;

namespace OcharoCatalogue.Application.Catalogue.Publishing.Services;

/// <summary>Defines the checks a rendered artifact passes before a release keeps it.</summary>
public interface IDesignArtifactValidator
{
    /// <summary>
    /// Checks that <paramref name="bytes"/> is a complete PDF with the expected pages; fails as invalid input.
    /// </summary>
    /// <param name="bytes">The uploaded PDF.</param>
    /// <param name="expectedPages">The page sizes the saved design declares.</param>
    void ValidatePdf(byte[] bytes, IReadOnlyList<CataloguePdfPageSize> expectedPages);

    /// <summary>
    /// Checks that <paramref name="bytes"/> is a single decodable PNG of <paramref name="ratio"/>; fails as invalid
    /// input.
    /// </summary>
    /// <param name="bytes">The uploaded PNG.</param>
    /// <param name="ratio">The width-to-height ratio the saved page declares.</param>
    void ValidatePng(byte[] bytes, double ratio);
}
