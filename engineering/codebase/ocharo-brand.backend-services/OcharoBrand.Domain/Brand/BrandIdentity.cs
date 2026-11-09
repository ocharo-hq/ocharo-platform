namespace OcharoBrand.Domain.Brand;

/// <summary>Name, words, colours, type and official destinations.</summary>
public sealed record BrandIdentity
{
    public required string Name { get; init; }

    /// <summary>How the name is written (e.g. "Ocharo", never OCHARO); shown to every editor.</summary>
    public string? SpellingRule { get; init; }

    public LocalizedText Descriptor { get; init; } = new(null, null, null);
    public LocalizedText Tagline { get; init; } = new(null, null, null);
    public LocalizedText Location { get; init; } = new(null, null, null);
    public IReadOnlyList<BrandColor> Palette { get; init; } = [];
    public BrandTypography Typography { get; init; } = new("Georgia", "Arial", null);
    public IReadOnlyList<BrandLink> Links { get; init; } = [];
    public string? VoiceNotes { get; init; }
}
