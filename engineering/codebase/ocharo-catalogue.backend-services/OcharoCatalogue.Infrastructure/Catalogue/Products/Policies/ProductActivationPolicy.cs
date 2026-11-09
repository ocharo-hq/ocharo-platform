using OcharoCatalogue.Domain.Catalogue.Models;
using OcharoCatalogue.Infrastructure.Catalogue.Core.Policies;

namespace OcharoCatalogue.Infrastructure.Catalogue.Products.Policies;

/// <summary>Decides whether a product document may be active.</summary>
public static class ProductActivationPolicy
{
    /// <summary>Gets why <paramref name="document"/> cannot be active, or null when it can.</summary>
    /// <param name="document">The product document.</param>
    public static string? Problem(ProductDocument document)
    {
        if (document.Status != ProductStatus.Active)
        {
            return null;
        }

        if (!CatalogueLocales.All.Any(code => !string.IsNullOrWhiteSpace(document.Locales.For(code).Name)))
        {
            return "an active product needs a name";
        }

        if (ProductPublicationEligibility.ActiveImages(document).Count == 0)
        {
            return "an active product needs at least one active image";
        }

        return null;
    }
}
