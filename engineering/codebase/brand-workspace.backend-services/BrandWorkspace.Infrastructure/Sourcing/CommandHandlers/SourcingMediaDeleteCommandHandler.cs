using BrandWorkspace.Application.Sourcing.Commands;
using BrandWorkspace.Infrastructure.Foundation.Extensions;
using BrandWorkspace.Infrastructure.Sourcing.Services;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Infrastructure.Sourcing.CommandHandlers;

/// <summary>Handles <see cref="SourcingMediaDeleteCommand"/>.</summary>
public sealed class SourcingMediaDeleteCommandHandler(SourcingService service)
    : ICommandHandler<SourcingMediaDeleteCommand, AppResult<Guid>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<Guid>> HandleAsync(
        SourcingMediaDeleteCommand request,
        CancellationToken cancellationToken)
    {
        Result deleted = await service.DeleteMediaAsync(
            request.Access,
            request.MediaId,
            request.ExpectedRevision,
            cancellationToken);
        return deleted.ToAppResult(request.MediaId);
    }
}
