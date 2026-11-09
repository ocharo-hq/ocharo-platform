using OcharoBrand.Application.Brand.Commands;
using OcharoBrand.Application.Brand.Errors;
using OcharoBrand.Application.Brand.Models;
using OcharoBrand.Application.Brand.Repositories;
using OcharoBrand.Domain.Brand;
using OcharoBrand.Infrastructure.Brand.FoundationServices;
using OcharoBrand.Infrastructure.Brand.Serializers;
using OcharoBrand.Infrastructure.Brand.Validators;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoBrand.Infrastructure.Brand.CommandHandlers;

/// <summary>Handles <see cref="ReleasePublishCommand"/>: freezes the draft as the next release, which every consumer following
/// "latest" picks up.</summary>
public sealed class ReleasePublishCommandHandler(IBrandRepository repository, BrandDraftService drafts, TimeProvider time)
    : ICommandHandler<ReleasePublishCommand, AppResult<ReleaseSummary>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<ReleaseSummary>> HandleAsync(ReleasePublishCommand request, CancellationToken ct)
    {
        var record = await repository.FindDraftAsync(ct);
        if (record is null)
            return AppResult<ReleaseSummary>.Fail(BrandErrors.NotFound("Brand draft"));
        if (record.Revision != request.ExpectedRevision)
            return AppResult<ReleaseSummary>.Fail(BrandErrors.Conflict("The draft changed since you opened it. Reload before publishing."));
        var draft = BrandJson.Read<BrandDraft>(record.Json);
        var versions = (await drafts.LogoVersionsAsync(ct)).ToDictionary(v => v.Id);
        var logos = draft.CurrentLogos.Where(p => versions.ContainsKey(p.Value)).ToDictionary(p => p.Key, p => versions[p.Value]);
        if (!logos.ContainsKey(LogoSlots.OnLight) && !logos.ContainsKey(LogoSlots.Master))
            return AppResult<ReleaseSummary>.Fail(BrandErrors.Invalid("Choose a logo for the light-surface or master slot before publishing."));
        var latest = await repository.FindLatestReleaseAsync(ct);
        var changes = latest is null
            ? ["Birinchi nashr"]
            : BrandDraftService.Differences(BrandJson.Read<BrandRelease>(latest.Json), draft, versions.Values.ToList());
        if (latest is not null && changes.Count == 0)
            return AppResult<ReleaseSummary>.Fail(BrandErrors.Conflict("Nothing changed since the last release."));
        if (!BrandInputValidator.TryClean(request.Note, 500, out var note))
            return AppResult<ReleaseSummary>.Fail(BrandErrors.TextTooLong(500));

        var release = new BrandRelease((latest?.Version ?? 0) + 1, time.GetUtcNow(), note ?? string.Join("; ", changes), draft.Identity, logos);
        var inserted = await repository.InsertReleaseAsync(
            new ReleaseRecord { Version = release.Version, PublishedAt = release.PublishedAt, Json = BrandJson.Write(release) }, ct);
        if (inserted is Result<ReleaseRecord>.Failure conflict)
            return AppResult<ReleaseSummary>.Fail(conflict.Error);
        return AppResult<ReleaseSummary>.Ok(new ReleaseSummary(release.Version, release.PublishedAt, release.Note, changes));
    }
}
