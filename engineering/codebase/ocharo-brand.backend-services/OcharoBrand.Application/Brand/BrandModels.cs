using OcharoBrand.Domain.Brand;

namespace OcharoBrand.Application.Brand;

public sealed record DraftView(BrandDraft Draft, int Revision, DateTimeOffset UpdatedAt, IReadOnlyList<LogoVersion> Logos, int? LatestRelease, IReadOnlyList<string> UnpublishedChanges);

public sealed record IdentityInput(int ExpectedRevision, BrandIdentity Identity);

public sealed record CurrentLogoInput(int ExpectedRevision, string VersionId);

public sealed record PublishInput(int ExpectedRevision, string? Note);

public sealed record RevisionSummary(int Revision, DateTimeOffset SavedAt, string Summary);

public sealed record ReleaseSummary(int Version, DateTimeOffset PublishedAt, string Note, IReadOnlyList<string> Changes);

public sealed record AppearanceInput(string? Id, string Name, DateTimeOffset StartsAt, DateTimeOffset EndsAt, IReadOnlyDictionary<string, string>? Overrides, string? AccentHex, string? Note,
    string? LinkLabel = null, string? LinkUrl = null);

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

/// <summary>A logo slot: <see cref="Url"/> is stable (follows changes), <see cref="FileUrl"/> is this exact file.</summary>
public sealed record PublicLogo(string Url, string FileUrl, string? PreviewUrl, string ContentType, int? Width, int? Height, string Sha256, bool FromAppearance);

public sealed record PublicAppearance(string Id, string Name, DateTimeOffset StartsAt, DateTimeOffset EndsAt, string? AccentHex, string? LinkLabel, string? LinkUrl);

public sealed record ConsumerView(string App, DateTimeOffset LastSeenAt, int LastRelease, bool Current, string LastPath);
