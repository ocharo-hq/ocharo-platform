using System.Text.Json;
using OcharoCatalogue.Domain.Catalogue.Models;
using OcharoCatalogue.Infrastructure.Catalogue.Core.Policies;
using OcharoCatalogue.Infrastructure.Catalogue.Designs.Validators;
using OcharoCatalogue.Infrastructure.Catalogue.Publishing.Models;

namespace OcharoCatalogue.Infrastructure.Catalogue.Publishing.Policies;

/// <summary>Decides which pages and products a release of a design holds in the design's language.</summary>
public static class ReleaseSelectionPolicy
{
    /// <summary>
    /// Gets the PDF pages the release renders: active pages whose products and images are all confirmed.
    /// </summary>
    /// <param name="preparation">The design read for preparation.</param>
    public static IEnumerable<JsonElement> PdfPages(PreparationContext preparation)
    {
        if (!preparation.Definition.Outputs.Pdf)
        {
            return [];
        }

        return preparation.Snapshot.GetProperty("pages").EnumerateArray()
            .Where(page => CatalogueDesignValidator.Active(page)
                && References(page, preparation.Products).All(id => Eligible(preparation, id))
                && PageImagesEligible(page, preparation.Products));
    }

    /// <summary>Tells whether product <paramref name="id"/> may appear in the release's language.</summary>
    /// <param name="preparation">The design read for preparation.</param>
    /// <param name="id">The product id.</param>
    public static bool Eligible(PreparationContext preparation, string id)
    {
        return preparation.Products.TryGetValue(id, out var product)
            && ProductPublicationEligibility.Eligible(product, preparation.Design.Locale);
    }

    /// <summary>Gets the products a PDF page shows: those it names, else the owners of the images it places.</summary>
    /// <param name="page">The PDF page.</param>
    /// <param name="products">Every product document by id.</param>
    public static IReadOnlyList<string> References(
        JsonElement page,
        IReadOnlyDictionary<string, ProductDocument> products)
    {
        var hashes = page.GetProperty("elements").EnumerateArray()
            .Where(element => element.TryGetProperty("src", out _))
            .Select(element => CatalogueDesignValidator.ImageHash(element.GetProperty("src").GetString()!))
            .ToHashSet();
        var explicitIds = CatalogueDesignValidator.ReferencesOnPage(page);
        if (explicitIds.Count > 0)
        {
            return explicitIds;
        }

        return products.Values
            .Where(product => product.Media.Any(image => hashes.Contains(image.Sha256)))
            .Select(product => product.Id)
            .Distinct()
            .ToArray();
    }

    private static bool PageImagesEligible(JsonElement page, IReadOnlyDictionary<string, ProductDocument> products)
    {
        var references = References(page, products);
        var placed = page.GetProperty("elements").EnumerateArray()
            .Where(element => element.TryGetProperty("src", out _));
        foreach (var element in placed)
        {
            var hash = CatalogueDesignValidator.ImageHash(element.GetProperty("src").GetString()!);
            if (hash is null)
            {
                continue;
            }

            var owners = products.Values.Where(product => product.Media.Any(image => image.Sha256 == hash)).ToArray();
            var selectable = owners.Any(owner => references.Contains(owner.Id)
                && ProductPublicationEligibility.ActiveImages(owner).Any(image => image.Sha256 == hash));
            if (owners.Length == 0 || !selectable)
            {
                return false;
            }
        }

        return true;
    }
}
