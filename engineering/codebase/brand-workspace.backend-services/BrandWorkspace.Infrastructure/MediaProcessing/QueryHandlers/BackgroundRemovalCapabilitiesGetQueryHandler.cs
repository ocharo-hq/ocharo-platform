using BrandWorkspace.Application.Foundation.Errors;
using BrandWorkspace.Application.MediaProcessing.Queries;
using WoW.Two.Sdk.Beta.Be.Media.BackgroundRemoval;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Infrastructure.MediaProcessing.QueryHandlers;

/// <summary>Handles <see cref="BackgroundRemovalCapabilitiesGetQuery"/>.</summary>
public sealed class BackgroundRemovalCapabilitiesGetQueryHandler(IBackgroundRemovalService worker)
    : IQueryHandler<BackgroundRemovalCapabilitiesGetQuery, AppResult<BackgroundRemovalStatus>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<BackgroundRemovalStatus>> HandleAsync(
        BackgroundRemovalCapabilitiesGetQuery request,
        CancellationToken cancellationToken)
    {
        return AppResult<BackgroundRemovalStatus>.Ok(await worker.GetStatusAsync(cancellationToken));
    }
}
