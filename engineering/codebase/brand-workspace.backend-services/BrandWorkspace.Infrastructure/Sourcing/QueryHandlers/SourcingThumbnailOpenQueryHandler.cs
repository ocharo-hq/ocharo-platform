using BrandWorkspace.Application.Files.Models;
using BrandWorkspace.Application.Sourcing.Queries;
using BrandWorkspace.Infrastructure.Foundation.Extensions;
using BrandWorkspace.Infrastructure.Sourcing.Services;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Infrastructure.Sourcing.QueryHandlers;

/// <summary>Handles <see cref="SourcingThumbnailOpenQuery"/>.</summary>
public sealed class SourcingThumbnailOpenQueryHandler(SourcingService service)
    : IQueryHandler<SourcingThumbnailOpenQuery, AppResult<StoredContent>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<StoredContent>> HandleAsync(
        SourcingThumbnailOpenQuery request,
        CancellationToken cancellationToken)
    {
        Result<StoredContent> opened = await service.OpenThumbnailAsync(
            request.Access,
            request.MediaId,
            cancellationToken);
        return opened.ToAppResult();
    }
}
