using OcharoBrand.Application.Brand.Errors;
using OcharoBrand.Application.Brand.Models;
using OcharoBrand.Application.Brand.Queries;
using OcharoBrand.Application.Brand.Services;
using OcharoBrand.Infrastructure.Brand.Extensions;
using OcharoBrand.Infrastructure.Brand.FoundationServices;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoBrand.Infrastructure.Brand.QueryHandlers;

/// <summary>Handles <see cref="PublicMediaOpenQuery"/>; an undisclosed file is not found, like a missing one.</summary>
public sealed class PublicMediaOpenQueryHandler(PublicBrandService kit, IMediaStore media)
    : IQueryHandler<PublicMediaOpenQuery, AppResult<MediaContent>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<MediaContent>> HandleAsync(PublicMediaOpenQuery request, CancellationToken ct)
    {
        if (!await kit.IsDisclosedAsync(request.Sha256, ct))
            return AppResult<MediaContent>.Fail(BrandErrors.NotFound("Media"));
        return (await media.OpenAsync(request.Sha256, request.Preview, ct)).ToAppResult();
    }
}
