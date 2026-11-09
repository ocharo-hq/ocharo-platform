using OcharoBrand.Application.Brand.Constants;
using OcharoBrand.Application.Brand.Errors;
using OcharoBrand.Application.Brand.Models;
using OcharoBrand.Application.Brand.Repositories;
using OcharoBrand.Domain.Brand;
using OcharoBrand.Infrastructure.Brand.Serializers;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace OcharoBrand.Infrastructure.Brand.FoundationServices;

/// <summary>
/// Reads the single revision-checked draft the way every editing handler answers it — its logo versions and what the next
/// release would change — and builds the row a draft save writes.
/// </summary>
public sealed class BrandDraftService(IBrandRepository repository, TimeProvider time)
{
    /// <summary>The draft with its logo versions (slot order, newest first) and its unpublished changes.</summary>
    public async Task<Result<DraftView>> GetViewAsync(CancellationToken ct)
    {
        var record = await repository.FindDraftAsync(ct);
        if (record is null)
            return Result<DraftView>.Fail(BrandErrors.NotFound("Brand draft"));
        var draft = BrandJson.Read<BrandDraft>(record.Json);
        var logos = (await LogoVersionsAsync(ct))
            .OrderBy(v => LogoSlots.All.ToList().IndexOf(v.Slot)).ThenByDescending(v => v.UploadedAt).ToList();
        var latest = await repository.FindLatestReleaseAsync(ct);
        var changes = latest is null
            ? ["Hali nashr qilinmagan"]
            : Differences(BrandJson.Read<BrandRelease>(latest.Json), draft, logos);
        return Result<DraftView>.Ok(new DraftView(draft, record.Revision, record.UpdatedAt, logos, latest?.Version, changes));
    }

    /// <summary>Every uploaded logo version.</summary>
    public async Task<IReadOnlyList<LogoVersion>> LogoVersionsAsync(CancellationToken ct)
    {
        return (await repository.GetLogoVersionsAsync(ct)).Select(r => BrandJson.Read<LogoVersion>(r.Json)).ToList();
    }

    /// <summary>The row that stores <paramref name="draft"/> as <paramref name="revision"/>, stamped now.</summary>
    public DraftRecord Record(BrandDraft draft, int revision)
    {
        return new DraftRecord { Id = BrandDraftConstants.DraftId, Revision = revision, UpdatedAt = time.GetUtcNow(), Json = BrandJson.Write(draft) };
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
}
