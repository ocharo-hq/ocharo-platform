using BrandWorkspace.Domain.Commerce;

namespace BrandWorkspace.Application.Commerce;

/// <summary>One exact-language rule for public web products and prepared catalogue artifacts.</summary>
public static class ProductPublicationEligibility
{
    public static bool Eligible(CommerceProduct product, string locale) =>
        product.Document.Status == "active" && CatalogueLanguages.Copy(product, locale).Status == "approved"
        && product.Document.Issues.All(issue => issue.Status != "open");
}
