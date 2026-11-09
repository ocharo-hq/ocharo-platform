namespace OcharoCatalogue.Domain.Catalogue.Models;

/// <summary>Catalogue languages in display order.</summary>
public static class CatalogueLocales
{
    public static readonly IReadOnlyList<string> All =
        ["uz", "ru", "en", "tg", "kk", "ky", "tk", "tr", "az", "fr", "it", "de", "es", "pt", "pl", "uk", "ar", "fa", "ur", "zh-Hans", "ko", "ja", "id", "ms", "hi"];
    public static bool Supported(string? locale) => locale is not null && All.Contains(locale, StringComparer.Ordinal);
    public static string Direction(string locale) => locale is "ar" or "fa" or "ur" ? "rtl" : "ltr";
}
