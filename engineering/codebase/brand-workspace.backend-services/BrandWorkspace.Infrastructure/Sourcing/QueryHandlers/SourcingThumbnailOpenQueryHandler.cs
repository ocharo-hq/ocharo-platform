using BrandWorkspace.Application.Files.Models;
using BrandWorkspace.Application.Sourcing.Queries;
using BrandWorkspace.Infrastructure.Sourcing.Services;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Infrastructure.Sourcing.QueryHandlers;

/// <summary>Handles <see cref="SourcingThumbnailOpenQuery"/>.</summary>
public sealed class SourcingThumbnailOpenQueryHandler(SourcingService service)
    : IQueryHandler<SourcingThumbnailOpenQuery, AppResult<StoredContent>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<StoredContent>> HandleAsync(SourcingThumbnailOpenQuery request, CancellationToken cancellationToken)
    {
        return AppResult<StoredContent>.Ok(await service.OpenThumbnailAsync(request.Access, request.MediaId, cancellationToken));
    }
}
