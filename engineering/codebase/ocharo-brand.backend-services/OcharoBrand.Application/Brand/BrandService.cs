using System.Text.RegularExpressions;
using OcharoBrand.Application.Abstractions;
using OcharoBrand.Domain.Brand;

namespace OcharoBrand.Application.Brand;

/// <summary>
/// The single place brand identity and logos change: a revision-checked draft, logo versions per slot, immutable
/// releases that consumers read, and scheduled appearances that temporarily override logos.
/// </summary>
public sealed partial class BrandService(IBrandRepository repository, IMediaStore media, IClock clock)
{
    public const string DraftId = "brand";

    public async Task<DraftView> GetAsync(CancellationToken ct)
    {
        var record = await repository.FindDraftAsync(ct) ?? throw BrandException.NotFound("Brand draft");
        var draft = BrandJson.Read<BrandDraft>(record.Json);
        var logos = (await repository.GetLogoVersionsAsync(ct)).Select(r => BrandJson.Read<LogoVersion>(r.Json))
            .OrderBy(v => LogoSlots.All.ToList().IndexOf(v.Slot)).ThenByDescending(v => v.UploadedAt).ToList();
        var latest = await repository.FindLatestReleaseAsync(ct);
        var changes = latest is null
            ? ["Hali nashr qilinmagan"]
            : Differences(BrandJson.Read<BrandRelease>(latest.Json), draft, logos);
        return new DraftView(draft, record.Revision, record.UpdatedAt, logos, latest?.Version, changes);
    }

    public async Task<DraftView> SaveIdentityAsync(IdentityInput input, CancellationToken ct)
    {
        var record = await repository.FindDraftAsync(ct) ?? throw BrandException.NotFound("Brand draft");
        var draft = BrandJson.Read<BrandDraft>(record.Json);
        var next = draft with { Identity = Normalize(input.Identity) };
        await repository.SaveDraftAsync(Record(next, input.ExpectedRevision + 1), input.ExpectedRevision, "Identity edited", ct);
        return await GetAsync(ct);
    }

    public async Task<LogoVersion> UploadLogoAsync(string slot, byte[] bytes, string fileName, string? note, CancellationToken ct, string? sourcePath = null,
        string status = LogoStatus.Draft)
    {
        if (!LogoSlots.All.Contains(slot)) throw new BrandException($"Unknown logo slot '{slot}'.");
        var stored = await media.StoreAsync(bytes, ct);
        var existing = (await repository.GetLogoVersionsAsync(ct)).Select(r => BrandJson.Read<LogoVersion>(r.Json))
            .FirstOrDefault(v => v.Slot == slot && v.Sha256 == stored.Sha256);
        if (existing is not null) return existing;
        var version = new LogoVersion(Guid.NewGuid().ToString("N")[..12], slot, stored.Sha256, SafeName(fileName), stored.ContentType, stored.ByteLength,
            stored.Width, stored.Height, stored.PreviewPath is not null, status, Clean(note, 500), sourcePath, clock.UtcNow);
        await repository.SaveLogoVersionAsync(new LogoVersionRecord { Id = version.Id, Slot = slot, Sha256 = version.Sha256, UploadedAt = version.UploadedAt,
            Json = BrandJson.Write(version) }, ct);
        return version;
    }

    public async Task<LogoVersion> SetLogoStatusAsync(string versionId, string status, CancellationToken ct)
    {
        if (!LogoStatus.All.Contains(status)) throw new BrandException("Unknown logo status.");
        var record = await repository.FindLogoVersionAsync(versionId, ct) ?? throw BrandException.NotFound("Logo version");
        var version = BrandJson.Read<LogoVersion>(record.Json) with { Status = status };
        record.Json = BrandJson.Write(version);
        await repository.SaveLogoVersionAsync(record, ct);
        return version;
    }

    /// <summary>Points a slot at a version in the draft; consumers see it after the next release.</summary>
    public async Task<DraftView> SetCurrentLogoAsync(string slot, CurrentLogoInput input, CancellationToken ct)
    {
        var version = await repository.FindLogoVersionAsync(input.VersionId, ct) ?? throw BrandException.NotFound("Logo version");
        if (version.Slot != slot) throw new BrandException("That file belongs to a different slot.");
        var record = await repository.FindDraftAsync(ct) ?? throw BrandException.NotFound("Brand draft");
        var draft = BrandJson.Read<BrandDraft>(record.Json);
        var logos = new Dictionary<string, string>(draft.CurrentLogos) { [slot] = version.Id };
        await repository.SaveDraftAsync(Record(draft with { CurrentLogos = logos }, input.ExpectedRevision + 1), input.ExpectedRevision,
            $"Logo {slot} → {BrandJson.Read<LogoVersion>(version.Json).FileName}", ct);
        return await GetAsync(ct);
    }

    public async Task<IReadOnlyList<RevisionSummary>> DraftHistoryAsync(CancellationToken ct) =>
        (await repository.GetDraftRevisionsAsync(ct)).OrderByDescending(r => r.Revision).Select(r => new RevisionSummary(r.Revision, r.SavedAt, r.Summary)).ToList();

    /// <summary>Freezes the draft as the next release; every consumer following "latest" picks it up.</summary>
    public async Task<ReleaseSummary> PublishAsync(PublishInput input, CancellationToken ct)
    {
        var record = await repository.FindDraftAsync(ct) ?? throw BrandException.NotFound("Brand draft");
        if (record.Revision != input.ExpectedRevision) throw BrandException.Conflict("The draft changed since you opened it. Reload before publishing.");
        var draft = BrandJson.Read<BrandDraft>(record.Json);
        var versions = (await repository.GetLogoVersionsAsync(ct)).Select(r => BrandJson.Read<LogoVersion>(r.Json)).ToDictionary(v => v.Id);
        var logos = draft.CurrentLogos.Where(p => versions.ContainsKey(p.Value)).ToDictionary(p => p.Key, p => versions[p.Value]);
        if (!logos.ContainsKey(LogoSlots.OnLight) && !logos.ContainsKey(LogoSlots.Master))
            throw new BrandException("Choose a logo for the light-surface or master slot before publishing.");
        var latest = await repository.FindLatestReleaseAsync(ct);
        var changes = latest is null ? ["Birinchi nashr"] : Differences(BrandJson.Read<BrandRelease>(latest.Json), draft, versions.Values.ToList());
        if (latest is not null && changes.Count == 0) throw BrandException.Conflict("Nothing changed since the last release.");
        var release = new BrandRelease((latest?.Version ?? 0) + 1, clock.UtcNow, Clean(input.Note, 500) ?? string.Join("; ", changes), draft.Identity, logos);
        await repository.InsertReleaseAsync(new ReleaseRecord { Version = release.Version, PublishedAt = release.PublishedAt, Json = BrandJson.Write(release) }, ct);
        return new ReleaseSummary(release.Version, release.PublishedAt, release.Note, changes);
    }

    public async Task<IReadOnlyList<ReleaseSummary>> ReleasesAsync(CancellationToken ct)
    {
        var releases = (await repository.GetReleasesAsync(ct)).OrderBy(r => r.Version).Select(r => BrandJson.Read<BrandRelease>(r.Json)).ToList();
        return releases.Select((r, i) => new ReleaseSummary(r.Version, r.PublishedAt, r.Note,
                i == 0 ? ["Birinchi nashr"] : Differences(releases[i - 1], new BrandDraft { Identity = r.Identity, CurrentLogos = r.Logos.ToDictionary(p => p.Key, p => p.Value.Id) }, r.Logos.Values.ToList())))
            .Reverse().ToList();
    }

    public async Task<IReadOnlyList<BrandAppearance>> AppearancesAsync(CancellationToken ct) =>
        (await repository.GetAppearancesAsync(ct)).Select(r => BrandJson.Read<BrandAppearance>(r.Json)).OrderByDescending(a => a.StartsAt).ToList();

    public async Task<BrandAppearance> SaveAppearanceAsync(AppearanceInput input, CancellationToken ct)
    {
        var name = Clean(input.Name, 120) ?? throw new BrandException("An appearance needs a name.");
        if (input.EndsAt <= input.StartsAt) throw new BrandException("The end must be after the start.");
        if (input.EndsAt - input.StartsAt > TimeSpan.FromDays(92)) throw new BrandException("Keep an appearance to three months or less.");
        if (input.AccentHex is { Length: > 0 } hex && !Hex().IsMatch(hex)) throw new BrandException("Accent colour must be #RRGGBB.");
        var overrides = new Dictionary<string, string>();
        foreach (var (slot, versionId) in input.Overrides ?? new Dictionary<string, string>())
        {
            if (string.IsNullOrWhiteSpace(versionId)) continue;
            var version = await repository.FindLogoVersionAsync(versionId, ct) ?? throw new BrandException($"Unknown logo version for {slot}.");
            if (version.Slot != slot) throw new BrandException($"The {slot} override uses a file from another slot.");
            overrides[slot] = versionId;
        }

        var linkLabel = Clean(input.LinkLabel, 60);
        var linkUrl = Clean(input.LinkUrl, 500);
        if ((linkLabel is null) != (linkUrl is null)) throw new BrandException("An event link needs both a label and an address.");
        if (linkUrl is not null && !(Uri.TryCreate(linkUrl, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps))
            throw new BrandException("The event link must be a full https:// address.");
        if (overrides.Count == 0 && string.IsNullOrWhiteSpace(input.AccentHex) && linkUrl is null)
            throw new BrandException("Choose a logo override, an accent colour or an event link.");
        var appearance = new BrandAppearance
        {
            Id = string.IsNullOrWhiteSpace(input.Id) ? Guid.NewGuid().ToString("N")[..10] : input.Id,
            Name = name,
            StartsAt = input.StartsAt.ToUniversalTime(),
            EndsAt = input.EndsAt.ToUniversalTime(),
            Overrides = overrides,
            AccentHex = string.IsNullOrWhiteSpace(input.AccentHex) ? null : input.AccentHex.ToLowerInvariant(),
            Note = Clean(input.Note, 500),
            LinkLabel = linkLabel,
            LinkUrl = linkUrl,
        };
        var overlapping = (await AppearancesAsync(ct)).FirstOrDefault(a => a.Id != appearance.Id && a.Status == AppearanceStatus.Scheduled
            && a.StartsAt < appearance.EndsAt && appearance.StartsAt < a.EndsAt);
        if (overlapping is not null) throw BrandException.Conflict($"Overlaps “{overlapping.Name}”. Change the dates or cancel it first.");
        await repository.SaveAppearanceAsync(ToRecord(appearance), ct);
        return appearance;
    }

    public async Task<BrandAppearance> CancelAppearanceAsync(string id, CancellationToken ct)
    {
        var record = await repository.FindAppearanceAsync(id, ct) ?? throw BrandException.NotFound("Appearance");
        var appearance = BrandJson.Read<BrandAppearance>(record.Json) with { Status = AppearanceStatus.Cancelled };
        await repository.SaveAppearanceAsync(ToRecord(appearance), ct);
        return appearance;
    }

    /// <summary>Human-readable differences between a release and the draft (Uzbek labels for the operator).</summary>
    public static List<string> Differences(BrandRelease release, BrandDraft draft, IReadOnlyList<LogoVersion> versions)
    {
        var changes = new List<string>();
        var a = release.Identity;
        var b = draft.Identity;
        if (a.Name != b.Name) changes.Add($"Nom: {a.Name} → {b.Name}");
        if (a.Descriptor != b.Descriptor || a.Tagline != b.Tagline || a.Location != b.Location || a.SpellingRule != b.SpellingRule || a.VoiceNotes != b.VoiceNotes)
            changes.Add("Matnlar");
        if (!a.Palette.SequenceEqual(b.Palette)) changes.Add("Ranglar");
        if (a.Typography != b.Typography) changes.Add("Shriftlar");
        if (!a.Links.SequenceEqual(b.Links)) changes.Add("Havolalar");
        var byId = versions.ToDictionary(v => v.Id);
        foreach (var slot in LogoSlots.All)
        {
            var before = release.Logos.GetValueOrDefault(slot)?.Id;
            var after = draft.CurrentLogos.GetValueOrDefault(slot);
            if (before != after) changes.Add($"Logotip {slot}: {(after is not null && byId.TryGetValue(after, out var v) ? v.FileName : "olib tashlangan")}");
        }

        return changes;
    }

    public DraftRecord Record(BrandDraft draft, int revision) => new() { Id = DraftId, Revision = revision, UpdatedAt = clock.UtcNow, Json = BrandJson.Write(draft) };

    private static AppearanceRecord ToRecord(BrandAppearance a) =>
        new() { Id = a.Id, StartsAt = a.StartsAt, EndsAt = a.EndsAt, Status = a.Status, Json = BrandJson.Write(a) };

    private static BrandIdentity Normalize(BrandIdentity identity)
    {
        var name = Clean(identity.Name, 60) ?? throw new BrandException("The brand needs a name.");
        var palette = identity.Palette.Select(c =>
        {
            var key = (c.Key ?? "").Trim().ToLowerInvariant();
            if (!Key().IsMatch(key)) throw new BrandException($"Colour key '{c.Key}' must be a short lowercase slug.");
            if (!Hex().IsMatch(c.Hex ?? "")) throw new BrandException($"Colour {key} must be #RRGGBB.");
            return new BrandColor(key, Clean(c.Name, 60) ?? key, c.Hex!.ToLowerInvariant(), Clean(c.Role, 40) ?? "accent");
        }).ToList();
        if (palette.Select(c => c.Key).Distinct().Count() != palette.Count) throw new BrandException("Colour keys must be unique.");
        var links = identity.Links.Select(l =>
        {
            if (!Uri.TryCreate(l.Url?.Trim(), UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "tel" or "mailto"))
                throw new BrandException($"Link '{l.Label}' needs an https://, tel: or mailto: address.");
            return new BrandLink(Clean(l.Key, 40)?.ToLowerInvariant() ?? "link", Clean(l.Label, 60) ?? l.Key, Clean(l.Handle, 80), uri.ToString());
        }).ToList();
        return identity with
        {
            Name = name,
            SpellingRule = Clean(identity.SpellingRule, 1000),
            Descriptor = Text(identity.Descriptor),
            Tagline = Text(identity.Tagline),
            Location = Text(identity.Location),
            Palette = palette,
            Typography = new BrandTypography(Clean(identity.Typography.Display, 80) ?? "Georgia", Clean(identity.Typography.Body, 80) ?? "Arial",
                Clean(identity.Typography.Notes, 500)),
            Links = links,
            VoiceNotes = Clean(identity.VoiceNotes, 2000),
        };

        static LocalizedText Text(LocalizedText? t) => new(Clean(t?.Uz, 200), Clean(t?.Ru, 200), Clean(t?.En, 200));
    }

    private static string? Clean(string? value, int max)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return null;
        if (trimmed.Length > max) throw new BrandException($"Text is longer than {max} characters.");
        return trimmed;
    }

    private static string SafeName(string fileName)
    {
        var name = Path.GetFileName(fileName.Replace('\\', '/'));
        return string.IsNullOrWhiteSpace(name) || name.Length > 150 ? "logo" : name;
    }

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex Hex();

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex Key();
}
