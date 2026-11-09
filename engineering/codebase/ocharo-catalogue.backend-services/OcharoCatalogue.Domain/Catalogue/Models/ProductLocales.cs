namespace OcharoCatalogue.Domain.Catalogue.Models;

/// <summary>Catalogue copy: original fields remain wire compatible; further languages use Additional.</summary>
public sealed record ProductLocales(ProductLocale Uz, ProductLocale Ru, ProductLocale En)
{
    public IReadOnlyDictionary<string, ProductLocale> Additional { get; init; } = new Dictionary<string, ProductLocale>(StringComparer.Ordinal);

    /// <summary>Returns exact-language copy; an absent translation is an empty draft, never another language.</summary>
    public ProductLocale For(string? locale) => locale switch
    {
        null or "uz" => Uz ?? ProductLocale.Empty(),
        "ru" => Ru ?? ProductLocale.Empty(),
        "en" => En ?? ProductLocale.Empty(),
        _ => Additional?.GetValueOrDefault(locale) ?? ProductLocale.Empty(),
    };

    public static ProductLocales Empty() => new(ProductLocale.Empty(), ProductLocale.Empty(), ProductLocale.Empty());
}
