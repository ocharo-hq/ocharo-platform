using BrandWorkspace.Application.Sourcing.Queries;
using BrandWorkspace.Domain.Sourcing.Models;
using BrandWorkspace.Infrastructure.Sourcing.Services;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Infrastructure.Sourcing.QueryHandlers;

/// <summary>Handles <see cref="SourcingMediaTotalsGetQuery"/>.</summary>
public sealed class SourcingMediaTotalsGetQueryHandler(SourcingService service)
    : IQueryHandler<SourcingMediaTotalsGetQuery, AppResult<SourcingMediaTotals>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<SourcingMediaTotals>> HandleAsync(SourcingMediaTotalsGetQuery request, CancellationToken cancellationToken)
    {
        return AppResult<SourcingMediaTotals>.Ok(await service.GetMediaTotalsAsync(request.Access, cancellationToken));
    }
}
