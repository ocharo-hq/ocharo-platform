using BrandWorkspace.Application.Preparation.Models;
using BrandWorkspace.Application.Preparation.Queries;
using BrandWorkspace.Infrastructure.Foundation.Extensions;
using BrandWorkspace.Infrastructure.Preparation.Services;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Infrastructure.Preparation.QueryHandlers;

/// <summary>Handles <see cref="PreparationMediaContentGetQuery"/>.</summary>
public sealed class PreparationMediaContentGetQueryHandler(PreparationService service)
    : IQueryHandler<PreparationMediaContentGetQuery, AppResult<MediaContent>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<MediaContent>> HandleAsync(
        PreparationMediaContentGetQuery request,
        CancellationToken cancellationToken)
    {
        Result<MediaContent> result = await service.GetMediaContentAsync(
            request.Access,
            request.MediaId,
            cancellationToken);
        return result.ToAppResult();
    }
}
