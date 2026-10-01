using System.Text;
using OcharoBrand.Application.Abstractions;
using OcharoBrand.Domain.Brand;

namespace OcharoBrand.Application.Brand;

/// <summary>
/// What every Ocharo app reads: the latest release with the active appearance applied. Stable logo URLs resolve
/// here, so one publish (or one scheduled appearance) changes the logo everywhere those URLs are embedded.
/// </summary>
public sealed class PublicBrandService(IBrandRepository repository, IClock clock)
{
    public async Task<BrandRelease?> LatestAsync(CancellationToken ct) =>
        await repository.FindLatestReleaseAsync(ct) is { } record ? BrandJson.Read<BrandRelease>(record.Json) : null;

    public async Task<BrandAppearance?> ActiveAppearanceAsync(CancellationToken ct)
    {
        var now = clock.UtcNow;
        return (await repository.GetAppearancesAsync(ct)).Select(r => BrandJson.Read<BrandAppearance>(r.Json))
            .Where(a => a.IsActive(now)).OrderByDescending(a => a.StartsAt).FirstOrDefault();
    }

    public async Task<PublicBrand> BuildAsync(string? locale, CancellationToken ct, int? version = null)
    {
        var release = version is { } v
            ? await repository.FindReleaseAsync(v, ct) is { } pinned ? BrandJson.Read<BrandRelease>(pinned.Json) : throw BrandException.NotFound("Release")
            : await LatestAsync(ct) ?? throw BrandException.NotFound("Published brand");
        // Pinned releases are exact snapshots; only "latest" carries today's appearance.
        var appearance = version is null ? await ActiveAppearanceAsync(ct) : null;
        var logos = new Dictionary<string, PublicLogo>();
        foreach (var slot in LogoSlots.All)
        {
            var (logo, fromAppearance) = await ResolveAsync(release, appearance, slot, ct);
            if (logo is null) continue;
            logos[slot] = new PublicLogo(BrandUrls.Logo(slot), BrandUrls.Media(logo.Sha256), logo.HasPreview ? BrandUrls.Media(logo.Sha256) + "/preview" : null,
                logo.ContentType, logo.Width, logo.Height, logo.Sha256, fromAppearance);
        }

        var code = locale is "ru" or "en" ? locale : "uz";
        var palette = appearance?.AccentHex is { } accent
            ? release.Identity.Palette.Select(c => c.Key == "accent" ? c with { Hex = accent } : c).ToList()
            : release.Identity.Palette;
        return new PublicBrand(release.Identity.Name, release.Identity.SpellingRule, release.Version, release.PublishedAt, code,
            release.Identity.Descriptor.For(code), release.Identity.Tagline.For(code), release.Identity.Location.For(code), release.Identity.Descriptor,
            palette, release.Identity.Typography, release.Identity.Links, logos,
            appearance is null ? null : new PublicAppearance(appearance.Id, appearance.Name, appearance.StartsAt, appearance.EndsAt, appearance.AccentHex,
                appearance.LinkLabel, appearance.LinkUrl),
            clock.UtcNow);
    }

    /// <summary>The logo a stable URL serves right now: an active appearance override, else the released file.</summary>
    public async Task<LogoVersion?> EffectiveLogoAsync(string slot, CancellationToken ct)
    {
        var release = await LatestAsync(ct);
        if (release is null) return null;
        return (await ResolveAsync(release, await ActiveAppearanceAsync(ct), slot, ct)).Logo;
    }

    /// <summary>
    /// Public media is only what has been disclosed: files of a published release, or of an appearance that has
    /// started. Draft uploads and future appearance artwork stay behind the admin API — a hash is not confidentiality.
    /// </summary>
    public async Task<bool> IsDisclosedAsync(string sha256, CancellationToken ct)
    {
        foreach (var record in await repository.GetReleasesAsync(ct))
            if (BrandJson.Read<BrandRelease>(record.Json).Logos.Values.Any(l => l.Sha256 == sha256)) return true;
        var now = clock.UtcNow;
        var started = (await repository.GetAppearancesAsync(ct)).Select(r => BrandJson.Read<BrandAppearance>(r.Json))
            .Where(a => a.Status == AppearanceStatus.Scheduled && a.StartsAt <= now);
        foreach (var id in started.SelectMany(a => a.Overrides.Values).Distinct())
            if (await repository.FindLogoVersionAsync(id, ct) is { } version && BrandJson.Read<LogoVersion>(version.Json).Sha256 == sha256) return true;
        return false;
    }

    /// <summary>Seconds until the kit changes on its own (an appearance starts or ends); caps public cache lifetimes.</summary>
    public async Task<int?> SecondsToNextBoundaryAsync(CancellationToken ct)
    {
        var now = clock.UtcNow;
        var next = (await repository.GetAppearancesAsync(ct)).Select(r => BrandJson.Read<BrandAppearance>(r.Json))
            .Where(a => a.Status == AppearanceStatus.Scheduled)
            .SelectMany(a => new[] { a.StartsAt, a.EndsAt })
            .Where(t => t > now).Order().Cast<DateTimeOffset?>().FirstOrDefault();
        return next is { } at ? (int)Math.Ceiling((at - now).TotalSeconds) : null;
    }

    /// <summary>Palette as CSS custom properties (<c>--ocharo-{key}</c>) plus the two type stacks.</summary>
    public async Task<string> TokensCssAsync(CancellationToken ct)
    {
        var brand = await BuildAsync(null, ct);
        var css = new StringBuilder($"/* {brand.Name} brand tokens · release {brand.Release} */\n:root {{\n");
        foreach (var color in brand.Palette) css.Append($"  --ocharo-{color.Key}: {color.Hex};\n");
        css.Append($"  --ocharo-font-display: {Stack(brand.Typography.Display)}, Georgia, serif;\n");
        css.Append($"  --ocharo-font-body: {Stack(brand.Typography.Body)}, Arial, sans-serif;\n");
        css.Append("}\n");
        return css.ToString();

        static string Stack(string font) => font.Contains(' ') ? $"\"{font.Replace("\"", "")}\"" : font;
    }

    public async Task TouchAsync(string? app, string path, int release, CancellationToken ct)
    {
        var name = app?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 40 || !name.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_')) return;
        await repository.TouchConsumerAsync(new ConsumerRecord { App = name.ToLowerInvariant(), LastSeenAt = clock.UtcNow, LastRelease = release, LastPath = path }, ct);
    }

    public async Task<IReadOnlyList<ConsumerView>> ConsumersAsync(CancellationToken ct)
    {
        var latest = (await repository.FindLatestReleaseAsync(ct))?.Version ?? 0;
        return (await repository.GetConsumersAsync(ct)).OrderBy(c => c.App)
            .Select(c => new ConsumerView(c.App, c.LastSeenAt, c.LastRelease, c.LastRelease == latest, c.LastPath)).ToList();
    }

    private async Task<(LogoVersion? Logo, bool FromAppearance)> ResolveAsync(BrandRelease release, BrandAppearance? appearance, string slot, CancellationToken ct)
    {
        if (appearance?.Overrides.GetValueOrDefault(slot) is { } overrideId && await repository.FindLogoVersionAsync(overrideId, ct) is { } record)
            return (BrandJson.Read<LogoVersion>(record.Json), true);
        var released = release.Logos.GetValueOrDefault(slot);
        // Slots without their own file fall back sensibly so every app gets an image.
        released ??= slot switch
        {
            LogoSlots.OnLight or LogoSlots.Social => release.Logos.GetValueOrDefault(LogoSlots.Master),
            LogoSlots.Favicon => release.Logos.GetValueOrDefault(LogoSlots.Avatar) ?? release.Logos.GetValueOrDefault(LogoSlots.OnLight),
            _ => null,
        };
        return (released, false);
    }
}
