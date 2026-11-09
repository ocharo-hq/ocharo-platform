using OcharoBrand.Application.Brand.Errors;
using OcharoBrand.Application.Brand.Models;
using OcharoBrand.Application.Brand.Queries;
using OcharoBrand.Application.Brand.Services;
using OcharoBrand.Infrastructure.Brand.FoundationServices;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoBrand.Infrastructure.Brand.QueryHandlers;

/// <summary>Handles <see cref="PublicLogoOpenQuery"/>: the effective file for the slot right now, with the latest release.</summary>
public sealed class PublicLogoOpenQueryHandler(PublicBrandService kit, IMediaStore media)
    : IQueryHandler<PublicLogoOpenQuery, AppResult<PublicLogoContent>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<PublicLogoContent>> HandleAsync(PublicLogoOpenQuery request, CancellationToken ct)
    {
        var logo = await kit.EffectiveLogoAsync(request.Slot, ct);
        if (logo is null)
            return AppResult<PublicLogoContent>.Fail(BrandErrors.NotFound("Logo"));
        var opened = await media.OpenAsync(logo.Sha256, request.Preview, ct);
        return await opened.Match(
            async content => AppResult<PublicLogoContent>.Ok(new PublicLogoContent(logo, content, (await kit.LatestAsync(ct))?.Version ?? 0)),
            error => Task.FromResult(AppResult<PublicLogoContent>.Fail(error)));
    }
}
