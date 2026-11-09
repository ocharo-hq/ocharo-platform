using BrandWorkspace.Application.Preparation.Queries;
using BrandWorkspace.Domain.Preparation.Models;
using BrandWorkspace.Infrastructure.Preparation.Services;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Infrastructure.Preparation.QueryHandlers;

/// <summary>Handles <see cref="PreparationMediaGetAllQuery"/>.</summary>
public sealed class PreparationMediaGetAllQueryHandler(PreparationService service)
    : IQueryHandler<PreparationMediaGetAllQuery, AppResult<IReadOnlyList<MediaAsset>>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<IReadOnlyList<MediaAsset>>> HandleAsync(
        PreparationMediaGetAllQuery request,
        CancellationToken cancellationToken)
    {
        return AppResult<IReadOnlyList<MediaAsset>>.Ok(await service.GetMediaAsync(request.Access, cancellationToken));
    }
}
