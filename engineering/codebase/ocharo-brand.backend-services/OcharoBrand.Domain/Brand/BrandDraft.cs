namespace OcharoBrand.Domain.Brand;

/// <summary>
/// The editable brand state: identity plus which logo version fills each slot. Consumers never read the draft;
/// they read the latest published <see cref="BrandRelease"/> (plus any active scheduled appearance).
/// </summary>
public sealed record BrandDraft
{
    public required BrandIdentity Identity { get; init; }

    /// <summary>Slot key → current logo version id.</summary>
    public IReadOnlyDictionary<string, string> CurrentLogos { get; init; } = new Dictionary<string, string>();
}
