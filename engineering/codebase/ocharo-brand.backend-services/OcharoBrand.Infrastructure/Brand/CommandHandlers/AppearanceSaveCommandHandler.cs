using OcharoBrand.Application.Brand.Commands;
using OcharoBrand.Application.Brand.Errors;
using OcharoBrand.Application.Brand.Repositories;
using OcharoBrand.Domain.Brand;
using OcharoBrand.Infrastructure.Brand.Mappers;
using OcharoBrand.Infrastructure.Brand.Serializers;
using OcharoBrand.Infrastructure.Brand.Validators;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoBrand.Infrastructure.Brand.CommandHandlers;

/// <summary>Handles <see cref="AppearanceSaveCommand"/>: an appearance keeps to three months, overrides only its own slots'
/// files and never overlaps another scheduled one.</summary>
public sealed class AppearanceSaveCommandHandler(IBrandRepository repository)
    : ICommandHandler<AppearanceSaveCommand, AppResult<BrandAppearance>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<BrandAppearance>> HandleAsync(AppearanceSaveCommand request, CancellationToken ct)
    {
        if (!BrandInputValidator.TryClean(request.Name, 120, out var name))
            return Fail(BrandErrors.TextTooLong(120));
        if (name is null)
            return Fail(BrandErrors.Invalid("An appearance needs a name."));
        if (request.EndsAt <= request.StartsAt)
            return Fail(BrandErrors.Invalid("The end must be after the start."));
        if (request.EndsAt - request.StartsAt > TimeSpan.FromDays(92))
            return Fail(BrandErrors.Invalid("Keep an appearance to three months or less."));
        if (request.AccentHex is { Length: > 0 } hex && !BrandInputValidator.IsHex(hex))
            return Fail(BrandErrors.Invalid("Accent colour must be #RRGGBB."));

        var overrides = new Dictionary<string, string>();
        foreach (var (slot, versionId) in request.Overrides ?? new Dictionary<string, string>())
        {
            if (string.IsNullOrWhiteSpace(versionId)) continue;
            var version = await repository.FindLogoVersionAsync(versionId, ct);
            if (version is null)
                return Fail(BrandErrors.Invalid($"Unknown logo version for {slot}."));
            if (version.Slot != slot)
                return Fail(BrandErrors.Invalid($"The {slot} override uses a file from another slot."));
            overrides[slot] = versionId;
        }

        if (!BrandInputValidator.TryClean(request.LinkLabel, 60, out var linkLabel))
            return Fail(BrandErrors.TextTooLong(60));
        if (!BrandInputValidator.TryClean(request.LinkUrl, 500, out var linkUrl))
            return Fail(BrandErrors.TextTooLong(500));
        if ((linkLabel is null) != (linkUrl is null))
            return Fail(BrandErrors.Invalid("An event link needs both a label and an address."));
        if (linkUrl is not null && !(Uri.TryCreate(linkUrl, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps))
            return Fail(BrandErrors.Invalid("The event link must be a full https:// address."));
        if (overrides.Count == 0 && string.IsNullOrWhiteSpace(request.AccentHex) && linkUrl is null)
            return Fail(BrandErrors.Invalid("Choose a logo override, an accent colour or an event link."));
        if (!BrandInputValidator.TryClean(request.Note, 500, out var note))
            return Fail(BrandErrors.TextTooLong(500));

        var appearance = new BrandAppearance
        {
            Id = string.IsNullOrWhiteSpace(request.Id) ? Guid.NewGuid().ToString("N")[..10] : request.Id,
            Name = name,
            StartsAt = request.StartsAt.ToUniversalTime(),
            EndsAt = request.EndsAt.ToUniversalTime(),
            Overrides = overrides,
            AccentHex = string.IsNullOrWhiteSpace(request.AccentHex) ? null : request.AccentHex.ToLowerInvariant(),
            Note = note,
            LinkLabel = linkLabel,
            LinkUrl = linkUrl,
        };
        var overlapping = (await repository.GetAppearancesAsync(ct)).Select(r => BrandJson.Read<BrandAppearance>(r.Json))
            .OrderByDescending(a => a.StartsAt)
            .FirstOrDefault(a => a.Id != appearance.Id && a.Status == AppearanceStatus.Scheduled
                && a.StartsAt < appearance.EndsAt && appearance.StartsAt < a.EndsAt);
        if (overlapping is not null)
            return Fail(BrandErrors.Conflict($"Overlaps “{overlapping.Name}”. Change the dates or cancel it first."));
        await repository.SaveAppearanceAsync(BrandRecordMapper.ToRecord(appearance), ct);
        return AppResult<BrandAppearance>.Ok(appearance);
    }

    private static AppResult<BrandAppearance> Fail(AppError error)
    {
        return AppResult<BrandAppearance>.Fail(error);
    }
}
