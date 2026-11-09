using BrandWorkspace.Application.Sourcing.Queries;
using BrandWorkspace.Domain.Sourcing.Models;
using BrandWorkspace.Infrastructure.Sourcing.Services;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Infrastructure.Sourcing.QueryHandlers;

/// <summary>Handles <see cref="SourcingMediaGetAllQuery"/>.</summary>
public sealed class SourcingMediaGetAllQueryHandler(SourcingService service)
    : IQueryHandler<SourcingMediaGetAllQuery, AppResult<IReadOnlyList<SourcingMedia>>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<IReadOnlyList<SourcingMedia>>> HandleAsync(SourcingMediaGetAllQuery request, CancellationToken cancellationToken)
    {
        var media = await service.GetMediaAsync(request.Access, request.SupplierId, request.Unassigned, request.Limit, request.Offset,
            cancellationToken);
        return AppResult<IReadOnlyList<SourcingMedia>>.Ok(media);
    }
}
