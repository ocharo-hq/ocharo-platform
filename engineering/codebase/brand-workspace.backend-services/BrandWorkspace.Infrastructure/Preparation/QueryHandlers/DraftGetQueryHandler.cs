using BrandWorkspace.Application.Preparation.Queries;
using BrandWorkspace.Domain.Preparation.Models;
using BrandWorkspace.Infrastructure.Preparation.Services;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Infrastructure.Preparation.QueryHandlers;

/// <summary>Handles <see cref="DraftGetQuery"/>.</summary>
public sealed class DraftGetQueryHandler(PreparationService service)
    : IQueryHandler<DraftGetQuery, AppResult<Draft>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<Draft>> HandleAsync(DraftGetQuery request, CancellationToken cancellationToken)
    {
        return AppResult<Draft>.Ok(await service.GetDraftAsync(request.Access, request.DraftId, cancellationToken));
    }
}
