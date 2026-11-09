using BrandWorkspace.Application.Sourcing.Commands;
using BrandWorkspace.Infrastructure.Sourcing.Services;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Infrastructure.Sourcing.CommandHandlers;

/// <summary>Handles <see cref="SourcingMediaAssignCommand"/>.</summary>
public sealed class SourcingMediaAssignCommandHandler(SourcingService service)
    : ICommandHandler<SourcingMediaAssignCommand, AppResult<int>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<int>> HandleAsync(SourcingMediaAssignCommand request, CancellationToken cancellationToken)
    {
        return AppResult<int>.Ok(await service.AssignMediaAsync(request.Access, request.MediaIds, request.SupplierId, cancellationToken));
    }
}
