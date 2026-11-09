using BrandWorkspace.Application.Preparation.Models;
using BrandWorkspace.Application.Preparation.Queries;
using BrandWorkspace.Infrastructure.Preparation.Services;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Infrastructure.Preparation.QueryHandlers;

/// <summary>Handles <see cref="PreparationMediaContentGetQuery"/>.</summary>
public sealed class PreparationMediaContentGetQueryHandler(PreparationService service)
    : IQueryHandler<PreparationMediaContentGetQuery, AppResult<MediaContent>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<MediaContent>> HandleAsync(PreparationMediaContentGetQuery request, CancellationToken cancellationToken)
    {
        return AppResult<MediaContent>.Ok(await service.GetMediaContentAsync(request.Access, request.MediaId, cancellationToken));
    }
}
