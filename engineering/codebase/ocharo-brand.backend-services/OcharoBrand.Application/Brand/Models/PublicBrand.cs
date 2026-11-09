using OcharoBrand.Domain.Brand;

namespace OcharoBrand.Application.Brand.Models;

/// <summary>The public brand kit: latest release with any active appearance applied.</summary>
public sealed record PublicBrand(
    string Name,
    string? SpellingRule,
    int Release,
    DateTimeOffset PublishedAt,
    string Locale,
    string? Descriptor,
    string? Tagline,
    string? Location,
    LocalizedText Descriptors,
    IReadOnlyList<BrandColor> Palette,
    BrandTypography Typography,
    IReadOnlyList<BrandLink> Links,
    IReadOnlyDictionary<string, PublicLogo> Logos,
    PublicAppearance? Appearance,
    DateTimeOffset GeneratedAt);
