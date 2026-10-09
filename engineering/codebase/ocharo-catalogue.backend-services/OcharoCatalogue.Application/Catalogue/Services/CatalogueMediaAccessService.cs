using OcharoCatalogue.Application.Catalogue.Models;
using OcharoCatalogue.Application.Catalogue.Repositories;
using OcharoCatalogue.Domain.Catalogue.Models;

namespace OcharoCatalogue.Application.Catalogue.Services;

/// <summary>Public media authority follows live approved products and published immutable releases.</summary>
public sealed class CatalogueMediaAccessService(ICatalogueRepository products, IDesignRepository designs)
{
    public async Task<bool> IsPublicAsync(string hash, CancellationToken ct)
    {
        if (hash.Length != 64 || hash.Any(character => !char.IsAsciiHexDigit(character))) return false;
        foreach (var row in await products.GetProductsAsync(ct))
        {
            if (row.Status != ProductStatus.Active) continue;
            var product = CatalogueJson.Read<ProductDocument>(row.Json);
            if (ProductPublicationEligibility.ActiveImages(product).Any(image => image.Sha256 == hash)
                && CatalogueLocales.All.Any(locale => ProductPublicationEligibility.Eligible(product, locale))) return true;
        }
        // Pinned historical releases remain accessible until explicitly withdrawn or their design is archived.
        foreach (var row in await designs.PublicReleasesAsync(ct))
        {
            var release = CatalogueJson.Read<CataloguePreparedRelease>(row.Json);
            if (release.Products.Any(product => product.Images.Any(image => image.Sha256 == hash))) return true;
        }
        return false;
    }
}
