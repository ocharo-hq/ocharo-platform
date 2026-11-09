using BrandWorkspace.Application.Preparation.Queries;
using BrandWorkspace.Domain.Preparation.Models;
using BrandWorkspace.Infrastructure.Preparation.Services;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Infrastructure.Preparation.QueryHandlers;

/// <summary>Handles <see cref="DraftGetAllQuery"/>.</summary>
public sealed class DraftGetAllQueryHandler(PreparationService service)
    : IQueryHandler<DraftGetAllQuery, AppResult<IReadOnlyList<DraftSummary>>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<IReadOnlyList<DraftSummary>>> HandleAsync(DraftGetAllQuery request, CancellationToken cancellationToken)
    {
        return AppResult<IReadOnlyList<DraftSummary>>.Ok(await service.GetDraftsAsync(request.Access, request.BrandId, cancellationToken));
    }
}
