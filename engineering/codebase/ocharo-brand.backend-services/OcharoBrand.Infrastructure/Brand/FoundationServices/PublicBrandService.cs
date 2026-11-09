using OcharoBrand.Application.Brand.Errors;
using OcharoBrand.Application.Brand.Models;
using OcharoBrand.Application.Brand.Repositories;
using OcharoBrand.Domain.Brand;
using OcharoBrand.Infrastructure.Brand.Serializers;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace OcharoBrand.Infrastructure.Brand.FoundationServices;

/// <summary>
/// What every Ocharo app reads: the latest release with the active appearance applied. Stable logo URLs resolve
/// here, so one publish (or one scheduled appearance) changes the logo everywhere those URLs are embedded.
/// </summary>
public sealed class PublicBrandService(IBrandRepository repository, TimeProvider time)
{
    /// <summary>The latest release; none before the first publish.</summary>
    public async Task<BrandRelease?> LatestAsync(CancellationToken ct)
    {
        return await repository.FindLatestReleaseAsync(ct) is { } record ? BrandJson.Read<BrandRelease>(record.Json) : null;
    }

    /// <summary>The scheduled appearance whose window holds now; the latest-starting one when several do.</summary>
    public async Task<BrandAppearance?> ActiveAppearanceAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();
        return (await repository.GetAppearancesAsync(ct)).Select(r => BrandJson.Read<BrandAppearance>(r.Json))
            .Where(a => a.IsActive(now)).OrderByDescending(a => a.StartsAt).FirstOrDefault();
    }

    /// <summary>The kit of the latest release with today's appearance, or of the pinned <paramref name="version"/>.</summary>
    public async Task<Result<PublicBrand>> BuildAsync(string? locale, int? version, CancellationToken ct)
    {
        BrandRelease release;
        if (version is { } pinnedVersion)
        {
            if (await repository.FindReleaseAsync(pinnedVersion, ct) is not { } pinned)
                return Result<PublicBrand>.Fail(BrandErrors.NotFound("Release"));
            release = BrandJson.Read<BrandRelease>(pinned.Json);
        }
        else
        {
            if (await LatestAsync(ct) is not { } latest)
                return Result<PublicBrand>.Fail(BrandErrors.NotFound("Published brand"));
            release = latest;
        }

        // Pinned releases are exact snapshots; only "latest" carries today's appearance.
        var appearance = version is null ? await ActiveAppearanceAsync(ct) : null;
        var logos = new Dictionary<string, PublicLogo>();
        var fileIds = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var slot in LogoSlots.All)
        {
            var (logo, fromAppearance) = await ResolveAsync(release, appearance, slot, ct);
            if (logo is null) continue;
            if (!fileIds.TryGetValue(logo.Sha256, out var fileId))
                fileIds[logo.Sha256] = fileId = (await repository.FindMediaAsync(logo.Sha256, ct))?.FileId;
            logos[slot] = new PublicLogo(BrandUrls.Logo(slot), BrandUrls.Media(logo.Sha256), logo.HasPreview ? BrandUrls.Media(logo.Sha256) + "/preview" : null,
                logo.ContentType, logo.Width, logo.Height, logo.Sha256, fromAppearance, fileId);
        }

        var code = locale is "ru" or "en" ? locale : "uz";
        var palette = appearance?.AccentHex is { } accent
            ? release.Identity.Palette.Select(c => c.Key == "accent" ? c with { Hex = accent } : c).ToList()
            : release.Identity.Palette;
        return Result<PublicBrand>.Ok(new PublicBrand(release.Identity.Name, release.Identity.SpellingRule, release.Version, release.PublishedAt, code,
            release.Identity.Descriptor.For(code), release.Identity.Tagline.For(code), release.Identity.Location.For(code), release.Identity.Descriptor,
            palette, release.Identity.Typography, release.Identity.Links, logos,
            appearance is null ? null : new PublicAppearance(appearance.Id, appearance.Name, appearance.StartsAt, appearance.EndsAt, appearance.AccentHex,
                appearance.LinkLabel, appearance.LinkUrl),
            time.GetUtcNow()));
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
        var now = time.GetUtcNow();
        var started = (await repository.GetAppearancesAsync(ct)).Select(r => BrandJson.Read<BrandAppearance>(r.Json))
            .Where(a => a.Status == AppearanceStatus.Scheduled && a.StartsAt <= now);
        foreach (var id in started.SelectMany(a => a.Overrides.Values).Distinct())
            if (await repository.FindLogoVersionAsync(id, ct) is { } version && BrandJson.Read<LogoVersion>(version.Json).Sha256 == sha256) return true;
        return false;
    }

    /// <summary>Seconds until the kit changes on its own (an appearance starts or ends); caps public cache lifetimes.</summary>
    public async Task<int?> SecondsToNextBoundaryAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var next = (await repository.GetAppearancesAsync(ct)).Select(r => BrandJson.Read<BrandAppearance>(r.Json))
            .Where(a => a.Status == AppearanceStatus.Scheduled)
            .SelectMany(a => new[] { a.StartsAt, a.EndsAt })
            .Where(t => t > now).Order().Cast<DateTimeOffset?>().FirstOrDefault();
        return next is { } at ? (int)Math.Ceiling((at - now).TotalSeconds) : null;
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
