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

/// <summary>Text in the three brand languages.</summary>
public sealed record LocalizedText(string? Uz, string? Ru, string? En)
{
    public string? For(string? locale) => locale switch { "ru" => Ru ?? Uz, "en" => En ?? Uz, _ => Uz };
}

/// <summary>A named palette colour; <see cref="Key"/> becomes the CSS token <c>--ocharo-{key}</c>.</summary>
public sealed record BrandColor(string Key, string Name, string Hex, string Role);

public sealed record BrandTypography(string Display, string Body, string? Notes);

/// <summary>An official destination; <see cref="Handle"/> keeps the account's exact spelling.</summary>
public sealed record BrandLink(string Key, string Label, string? Handle, string Url);

/// <summary>An uploaded logo file for one slot; bytes live in the content-addressed media store.</summary>
public sealed record LogoVersion(
    string Id,
    string Slot,
    string Sha256,
    string FileName,
    string ContentType,
    long ByteLength,
    int? Width,
    int? Height,
    bool HasPreview,
    string Status,
    string? Note,
    string? SourcePath,
    DateTimeOffset UploadedAt);

/// <summary>An immutable published brand kit. Apps can follow the latest or pin a version.</summary>
public sealed record BrandRelease(int Version, DateTimeOffset PublishedAt, string Note, BrandIdentity Identity, IReadOnlyDictionary<string, LogoVersion> Logos);

/// <summary>
/// A temporary appearance for an event (like a seasonal logo): inside its window, its slot overrides replace the
/// released logos everywhere the stable logo URLs are used. Printed or exported copies are unaffected.
/// </summary>
public sealed record BrandAppearance
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public DateTimeOffset StartsAt { get; init; }
    public DateTimeOffset EndsAt { get; init; }
    public string Status { get; init; } = AppearanceStatus.Scheduled;

    /// <summary>Slot key → logo version id used during the window.</summary>
    public IReadOnlyDictionary<string, string> Overrides { get; init; } = new Dictionary<string, string>();

    /// <summary>Optional accent colour during the window (becomes <c>--ocharo-accent</c>).</summary>
    public string? AccentHex { get; init; }

    public string? Note { get; init; }

    /// <summary>Optional event badge beside the regular logo (the logo itself keeps linking home).</summary>
    public string? LinkLabel { get; init; }

    /// <summary>Absolute https destination of the event badge.</summary>
    public string? LinkUrl { get; init; }

    public bool IsActive(DateTimeOffset at) => Status == AppearanceStatus.Scheduled && StartsAt <= at && at < EndsAt;
}

public static class AppearanceStatus
{
    public const string Scheduled = "scheduled";
    public const string Cancelled = "cancelled";
}

/// <summary>Logo slots every Ocharo app can ask for.</summary>
public static class LogoSlots
{
    public const string Master = "master";
    public const string OnLight = "on-light";
    public const string OnDark = "on-dark";
    public const string Avatar = "avatar";
    public const string Favicon = "favicon";
    public const string Social = "social";

    public static readonly IReadOnlyList<string> All = [Master, OnLight, OnDark, Avatar, Favicon, Social];
}

public static class LogoStatus
{
    public const string Draft = "draft";
    public const string Approved = "approved";

    public static readonly IReadOnlySet<string> All = new HashSet<string> { Draft, Approved };
}
