using BrandWorkspace.Domain.Commerce;
using OcharoCatalogue.Domain.Catalogue.Models;

namespace BrandWorkspace.Application.Commerce;

/// <summary>Supported catalogue languages, independent of the operator interface language.</summary>
public static class CatalogueLanguages
{
    public static IReadOnlyList<string> Codes { get; } = Array.AsReadOnly(new[]
    {
        "uz", "ru", "en", "tg", "kk", "ky", "tk", "tr", "az", "fr", "it", "de", "es",
        "pt", "pl", "uk", "ar", "fa", "ur", "zh-Hans", "ko", "ja", "id", "ms", "hi"
    });

    public static bool Supported(string? locale) => locale is not null && Codes.Contains(locale, StringComparer.Ordinal);
    public static bool Core(string locale) => locale is "uz" or "ru" or "en";

    /// <summary>Exact copy only; missing translations stay unpublished drafts instead of silently changing languages.</summary>
    public static ProductLocale Copy(CommerceProduct product, string locale) => Core(locale)
        ? product.Document.Locales.For(locale)
        : product.Translations?.GetValueOrDefault(locale) ?? ProductLocale.Empty();
}
