namespace OcharoCatalogue.Domain.Catalogue.Models;

/// <summary>Collection names per catalogue language.</summary>
public sealed record CollectionNames(string Uz, string? Ru, string? En)
{
    /// <summary>Returns the name for a locale, falling back to Uzbek.</summary>
    public string For(string? locale) => locale switch
    {
        "ru" when !string.IsNullOrWhiteSpace(Ru) => Ru!,
        "en" when !string.IsNullOrWhiteSpace(En) => En!,
        _ => Uz,
    };
}
