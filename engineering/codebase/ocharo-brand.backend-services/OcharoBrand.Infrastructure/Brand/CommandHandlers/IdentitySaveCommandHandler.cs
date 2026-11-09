using OcharoBrand.Application.Brand.Commands;
using OcharoBrand.Application.Brand.Errors;
using OcharoBrand.Application.Brand.Models;
using OcharoBrand.Application.Brand.Repositories;
using OcharoBrand.Domain.Brand;
using OcharoBrand.Infrastructure.Brand.Extensions;
using OcharoBrand.Infrastructure.Brand.FoundationServices;
using OcharoBrand.Infrastructure.Brand.Serializers;
using OcharoBrand.Infrastructure.Brand.Validators;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoBrand.Infrastructure.Brand.CommandHandlers;

/// <summary>Handles <see cref="IdentitySaveCommand"/>.</summary>
public sealed class IdentitySaveCommandHandler(IBrandRepository repository, BrandDraftService drafts)
    : ICommandHandler<IdentitySaveCommand, AppResult<DraftView>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<DraftView>> HandleAsync(IdentitySaveCommand request, CancellationToken ct)
    {
        var record = await repository.FindDraftAsync(ct);
        if (record is null)
            return AppResult<DraftView>.Fail(BrandErrors.NotFound("Brand draft"));
        var draft = BrandJson.Read<BrandDraft>(record.Json);
        if (Normalize(request.Identity).IsFailure(out var invalid, out var identity))
            return AppResult<DraftView>.Fail(invalid);
        var next = draft with { Identity = identity };
        var saved = await repository.SaveDraftAsync(drafts.Record(next, request.ExpectedRevision + 1), request.ExpectedRevision, "Identity edited", ct);
        if (saved is Result<DraftRecord>.Failure conflict)
            return AppResult<DraftView>.Fail(conflict.Error);
        return (await drafts.GetViewAsync(ct)).ToAppResult();
    }

    /// <summary>Trims and checks every field; the first refused one names the failure.</summary>
    private static Result<BrandIdentity> Normalize(BrandIdentity identity)
    {
        if (!BrandInputValidator.TryClean(identity.Name, 60, out var name))
            return TooLong(60);
        if (name is null)
            return Result<BrandIdentity>.Fail(BrandErrors.Invalid("The brand needs a name."));

        var palette = new List<BrandColor>();
        foreach (var color in identity.Palette)
        {
            var key = (color.Key ?? "").Trim().ToLowerInvariant();
            if (!BrandInputValidator.IsKey(key))
                return Result<BrandIdentity>.Fail(BrandErrors.Invalid($"Colour key '{color.Key}' must be a short lowercase slug."));
            if (!BrandInputValidator.IsHex(color.Hex ?? ""))
                return Result<BrandIdentity>.Fail(BrandErrors.Invalid($"Colour {key} must be #RRGGBB."));
            if (!BrandInputValidator.TryClean(color.Name, 60, out var colorName))
                return TooLong(60);
            if (!BrandInputValidator.TryClean(color.Role, 40, out var role))
                return TooLong(40);
            palette.Add(new BrandColor(key, colorName ?? key, color.Hex!.ToLowerInvariant(), role ?? "accent"));
        }

        if (palette.Select(c => c.Key).Distinct().Count() != palette.Count)
            return Result<BrandIdentity>.Fail(BrandErrors.Invalid("Colour keys must be unique."));

        var links = new List<BrandLink>();
        foreach (var link in identity.Links)
        {
            if (!Uri.TryCreate(link.Url?.Trim(), UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "tel" or "mailto"))
                return Result<BrandIdentity>.Fail(BrandErrors.Invalid($"Link '{link.Label}' needs an https://, tel: or mailto: address."));
            if (!BrandInputValidator.TryClean(link.Key, 40, out var linkKey))
                return TooLong(40);
            if (!BrandInputValidator.TryClean(link.Label, 60, out var label))
                return TooLong(60);
            if (!BrandInputValidator.TryClean(link.Handle, 80, out var handle))
                return TooLong(80);
            links.Add(new BrandLink(linkKey?.ToLowerInvariant() ?? "link", label ?? link.Key, handle, uri.ToString()));
        }

        if (!BrandInputValidator.TryClean(identity.SpellingRule, 1000, out var spellingRule))
            return TooLong(1000);
        if (Text(identity.Descriptor) is not { } descriptor || Text(identity.Tagline) is not { } tagline || Text(identity.Location) is not { } location)
            return TooLong(200);
        if (!BrandInputValidator.TryClean(identity.Typography.Display, 80, out var display))
            return TooLong(80);
        if (!BrandInputValidator.TryClean(identity.Typography.Body, 80, out var body))
            return TooLong(80);
        if (!BrandInputValidator.TryClean(identity.Typography.Notes, 500, out var notes))
            return TooLong(500);
        if (!BrandInputValidator.TryClean(identity.VoiceNotes, 2000, out var voiceNotes))
            return TooLong(2000);

        return Result<BrandIdentity>.Ok(identity with
        {
            Name = name,
            SpellingRule = spellingRule,
            Descriptor = descriptor,
            Tagline = tagline,
            Location = location,
            Palette = palette,
            Typography = new BrandTypography(display ?? "Georgia", body ?? "Arial", notes),
            Links = links,
            VoiceNotes = voiceNotes,
        });

        static Result<BrandIdentity> TooLong(int maximum) => Result<BrandIdentity>.Fail(BrandErrors.TextTooLong(maximum));

        // Each language up to 200 characters; none when any is longer.
        static LocalizedText? Text(LocalizedText? text) =>
            BrandInputValidator.TryClean(text?.Uz, 200, out var uz) && BrandInputValidator.TryClean(text?.Ru, 200, out var ru)
                && BrandInputValidator.TryClean(text?.En, 200, out var en)
                ? new LocalizedText(uz, ru, en)
                : null;
    }
}
