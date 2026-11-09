namespace OcharoCatalogue.Domain.Catalogue.Models;

/// <summary>Image groups shared by catalogue page selection and product editing.</summary>
public static class ProductMediaGroup
{
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal) { "product", "model", "detail", "fabric" };
    public static string For(ProductMedia image) => image.Group is not null && All.Contains(image.Group)
        ? image.Group : image.Role switch { MediaRole.Model => "model", MediaRole.Detail => "detail", MediaRole.Fabric => "fabric", _ => "product" };
}
