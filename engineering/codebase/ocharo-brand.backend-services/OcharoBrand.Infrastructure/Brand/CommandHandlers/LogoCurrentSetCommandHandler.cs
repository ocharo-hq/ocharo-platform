using OcharoBrand.Application.Brand.Commands;
using OcharoBrand.Application.Brand.Errors;
using OcharoBrand.Application.Brand.Models;
using OcharoBrand.Application.Brand.Repositories;
using OcharoBrand.Domain.Brand;
using OcharoBrand.Infrastructure.Brand.Extensions;
using OcharoBrand.Infrastructure.Brand.FoundationServices;
using OcharoBrand.Infrastructure.Brand.Serializers;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoBrand.Infrastructure.Brand.CommandHandlers;

/// <summary>Handles <see cref="LogoCurrentSetCommand"/>; consumers see the change after the next release.</summary>
public sealed class LogoCurrentSetCommandHandler(IBrandRepository repository, BrandDraftService drafts)
    : ICommandHandler<LogoCurrentSetCommand, AppResult<DraftView>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<DraftView>> HandleAsync(LogoCurrentSetCommand request, CancellationToken ct)
    {
        var version = await repository.FindLogoVersionAsync(request.VersionId, ct);
        if (version is null)
            return AppResult<DraftView>.Fail(BrandErrors.NotFound("Logo version"));
        if (version.Slot != request.Slot)
            return AppResult<DraftView>.Fail(BrandErrors.Invalid("That file belongs to a different slot."));
        var record = await repository.FindDraftAsync(ct);
        if (record is null)
            return AppResult<DraftView>.Fail(BrandErrors.NotFound("Brand draft"));
        var draft = BrandJson.Read<BrandDraft>(record.Json);
        var logos = new Dictionary<string, string>(draft.CurrentLogos) { [request.Slot] = version.Id };
        var saved = await repository.SaveDraftAsync(drafts.Record(draft with { CurrentLogos = logos }, request.ExpectedRevision + 1), request.ExpectedRevision,
            $"Logo {request.Slot} → {BrandJson.Read<LogoVersion>(version.Json).FileName}", ct);
        if (saved is Result<DraftRecord>.Failure conflict)
            return AppResult<DraftView>.Fail(conflict.Error);
        return (await drafts.GetViewAsync(ct)).ToAppResult();
    }
}
