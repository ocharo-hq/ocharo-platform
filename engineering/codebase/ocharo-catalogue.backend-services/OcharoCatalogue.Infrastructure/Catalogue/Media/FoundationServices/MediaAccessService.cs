using OcharoCatalogue.Application.Catalogue.Core.Repositories;
using OcharoCatalogue.Application.Catalogue.Core.Serializers;
using OcharoCatalogue.Application.Catalogue.Publishing.Models;
using OcharoCatalogue.Domain.Catalogue.Models;
using OcharoCatalogue.Infrastructure.Catalogue.Core.Policies;

namespace OcharoCatalogue.Infrastructure.Catalogue.Media.FoundationServices;

/// <summary>Provides public media authority: an image is public while it belongs to a live approved product or a
/// published immutable release.</summary>
public sealed class MediaAccessService(ICatalogueRepository products, IDesignRepository designs)
{
    /// <summary>Tells whether anyone may read the stored image <paramref name="hash"/>.</summary>
    /// <param name="hash">The image's SHA-256.</param>
    /// <param name="ct">The token that cancels the read.</param>
    public async Task<bool> IsPublicAsync(string hash, CancellationToken ct)
    {
        if (hash.Length != 64 || hash.Any(character => !char.IsAsciiHexDigit(character)))
        {
            return false;
        }

        foreach (var row in await products.GetProductsAsync(ct))
        {
            if (row.Status != ProductStatus.Active)
            {
                continue;
            }

            var product = CatalogueJson.Read<ProductDocument>(row.Json);
            if (ProductPublicationEligibility.ActiveImages(product).Any(image => image.Sha256 == hash)
                && CatalogueLocales.All.Any(locale => ProductPublicationEligibility.Eligible(product, locale)))
            {
                return true;
            }
        }

        // Pinned historical releases remain accessible until explicitly withdrawn or their design is archived.
        foreach (var row in await designs.PublicReleasesAsync(ct))
        {
            var release = CatalogueJson.Read<CataloguePreparedRelease>(row.Json);
            if (release.Products.Any(product => product.Images.Any(image => image.Sha256 == hash)))
            {
                return true;
            }
        }

        return false;
    }
}
