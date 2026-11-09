using OcharoBrand.Application.Brand.Models;
using OcharoBrand.Application.Brand.Queries;
using OcharoBrand.Application.Brand.Services;
using OcharoBrand.Infrastructure.Brand.Extensions;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoBrand.Infrastructure.Brand.QueryHandlers;

/// <summary>Handles <see cref="MediaOpenQuery"/>.</summary>
public sealed class MediaOpenQueryHandler(IMediaStore media) : IQueryHandler<MediaOpenQuery, AppResult<MediaContent>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<MediaContent>> HandleAsync(MediaOpenQuery request, CancellationToken ct)
    {
        return (await media.OpenAsync(request.Sha256, request.Preview, ct)).ToAppResult();
    }
}
