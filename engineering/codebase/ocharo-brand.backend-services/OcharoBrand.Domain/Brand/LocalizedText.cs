namespace OcharoBrand.Domain.Brand;

/// <summary>Text in the three brand languages.</summary>
public sealed record LocalizedText(string? Uz, string? Ru, string? En)
{
    public string? For(string? locale) => locale switch { "ru" => Ru ?? Uz, "en" => En ?? Uz, _ => Uz };
}
