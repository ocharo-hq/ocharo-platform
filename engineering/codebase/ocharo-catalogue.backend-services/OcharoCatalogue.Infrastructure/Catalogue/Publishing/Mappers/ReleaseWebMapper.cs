using System.Text.Json;
using OcharoCatalogue.Application.Catalogue.Core.Serializers;
using OcharoCatalogue.Application.Catalogue.Designs.Models;
using OcharoCatalogue.Application.Catalogue.Publishing.Models;
using OcharoCatalogue.Infrastructure.Catalogue.Core.Policies;
using OcharoCatalogue.Infrastructure.Catalogue.Designs.Validators;
using OcharoCatalogue.Infrastructure.Catalogue.Publishing.Models;

namespace OcharoCatalogue.Infrastructure.Catalogue.Publishing.Mappers;

/// <summary>Maps a design's active web pages to the frozen web output a release carries.</summary>
public static class ReleaseWebMapper
{
    /// <summary>Maps the web pages to the release's web output: theme, pages, image group, price flags, copy and the
    /// images each product shows.</summary>
    /// <param name="preparation">The design read for preparation.</param>
    /// <param name="pages">The active web pages with their confirmed products.</param>
    /// <param name="products">The release's public products.</param>
    public static JsonElement ToWeb(
        PreparationContext preparation,
        IReadOnlyList<CatalogueWebPage> pages,
        IReadOnlyList<PublicProduct> products)
    {
        var group = "all";
        var price = CatalogueDesignValidator.PriceFields.ToDictionary(key => key, _ => true);
        var copy = CatalogueDesignValidator.CopyFields.ToDictionary(key => key, _ => "");
        if (preparation.Snapshot.TryGetProperty("catalogueSettings", out var settings))
        {
            group = settings.GetProperty("mediaGroup").GetString()!;
            foreach (var key in price.Keys.ToArray())
            {
                price[key] = settings.GetProperty("priceDisplay").GetProperty(key).GetBoolean();
            }

            if (settings.GetProperty("copyByLocale").TryGetProperty(preparation.Design.Locale, out var selected))
            {
                foreach (var key in copy.Keys.ToArray())
                {
                    copy[key] = selected.GetProperty(key).GetString()!;
                }
            }
        }

        var media = products.ToDictionary(
            product => product.Id,
            product => ProductPublicationEligibility.ActiveImages(preparation.Products[product.Id])
                .Where(image => group == "all" || ProductPublicationEligibility.MediaGroup(image) == group)
                .Select(image => image.Sha256)
                .ToArray());
        var web = new
        {
            theme = preparation.Definition.Theme,
            pages = pages.Select(page => new { page.Id, page.Title, page.Layout, page.ProductIds }),
            mediaGroup = group,
            priceDisplay = price,
            copy,
            productMedia = media,
        };
        return JsonSerializer.SerializeToElement(web, CatalogueJson.Options);
    }
}
