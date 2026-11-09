using BrandWorkspace.Application.Files.Models;
using BrandWorkspace.Application.Sourcing.Queries;
using BrandWorkspace.Infrastructure.Sourcing.Services;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Infrastructure.Sourcing.QueryHandlers;

/// <summary>Handles <see cref="SourcingMediaOpenQuery"/>.</summary>
public sealed class SourcingMediaOpenQueryHandler(SourcingService service)
    : IQueryHandler<SourcingMediaOpenQuery, AppResult<StoredContent>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<StoredContent>> HandleAsync(SourcingMediaOpenQuery request, CancellationToken cancellationToken)
    {
        return AppResult<StoredContent>.Ok(await service.OpenMediaAsync(request.Access, request.MediaId, cancellationToken));
    }
}
