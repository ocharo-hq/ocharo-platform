namespace OcharoCatalogue.Domain.Catalogue.Models;

/// <summary>Product lifecycle: only <see cref="Active"/> products reach the public feed.</summary>
public static class ProductStatus
{
    public const string Draft = "draft";
    public const string Active = "active";
    public const string Archived = "archived";

    public static readonly IReadOnlySet<string> All = new HashSet<string> { Draft, Active, Archived };
}

/// <summary>Copy approval for one language.</summary>
public static class LocaleStatus
{
    public const string Draft = "draft";
    public const string Approved = "approved";

    public static readonly IReadOnlySet<string> All = new HashSet<string> { Draft, Approved };
}

/// <summary>Whether a price is the owner's confirmed figure or a working draft.</summary>
public static class PricingStatus
{
    public const string Draft = "draft";
    public const string Confirmed = "confirmed";

    public static readonly IReadOnlySet<string> All = new HashSet<string> { Draft, Confirmed };
}

/// <summary>Review question state.</summary>
public static class IssueStatus
{
    public const string Open = "open";
    public const string Resolved = "resolved";

    public static readonly IReadOnlySet<string> All = new HashSet<string> { Open, Resolved };
}

/// <summary>What an image shows; the first cutout is the default catalogue image.</summary>
public static class MediaRole
{
    public const string Cutout = "cutout";
    public const string Model = "model";
    public const string Reference = "reference";
    public const string Photo = "photo";
    public const string Detail = "detail";
    public const string Fabric = "fabric";

    public static readonly IReadOnlySet<string> All = new HashSet<string> { Cutout, Model, Reference, Photo, Detail, Fabric };
}

/// <summary>Catalogue languages in display order.</summary>
public static class CatalogueLocales
{
    public static readonly IReadOnlyList<string> All =
        ["uz", "ru", "en", "tg", "kk", "ky", "tk", "tr", "az", "fr", "it", "de", "es", "pt", "pl", "uk", "ar", "fa", "ur", "zh-Hans", "ko", "ja", "id", "ms", "hi"];
    public static bool Supported(string? locale) => locale is not null && All.Contains(locale, StringComparer.Ordinal);
    public static string Direction(string locale) => locale is "ar" or "fa" or "ur" ? "rtl" : "ltr";
}

/// <summary>Image groups shared by catalogue page selection and product editing.</summary>
public static class ProductMediaGroup
{
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal) { "product", "model", "detail", "fabric" };
    public static string For(ProductMedia image) => image.Group is not null && All.Contains(image.Group)
        ? image.Group : image.Role switch { MediaRole.Model => "model", MediaRole.Detail => "detail", MediaRole.Fabric => "fabric", _ => "product" };
}
