using BrandWorkspace.Application.Sourcing.Commands;
using BrandWorkspace.Infrastructure.Sourcing.Services;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Infrastructure.Sourcing.CommandHandlers;

/// <summary>Handles <see cref="SourcingMediaDeleteCommand"/>.</summary>
public sealed class SourcingMediaDeleteCommandHandler(SourcingService service)
    : ICommandHandler<SourcingMediaDeleteCommand, AppResult<Guid>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<Guid>> HandleAsync(SourcingMediaDeleteCommand request, CancellationToken cancellationToken)
    {
        await service.DeleteMediaAsync(request.Access, request.MediaId, request.ExpectedRevision, cancellationToken);
        return AppResult<Guid>.Ok(request.MediaId);
    }
}
