namespace OcharoBrand.Domain.Brand;

/// <summary>A named palette colour; <see cref="Key"/> becomes the CSS token <c>--ocharo-{key}</c>.</summary>
public sealed record BrandColor(string Key, string Name, string Hex, string Role);
