namespace OcharoCatalogue.Domain.Catalogue.Models;

/// <summary>A garment family with its names in the three catalogue languages.</summary>
public sealed record GarmentTypeDefinition(string Key, string Uz, string Ru, string En)
{
    /// <summary>Returns the family name for a locale.</summary>
    public string For(string? locale) => locale switch { "ru" => Ru, "en" => En, _ => Uz };
}
