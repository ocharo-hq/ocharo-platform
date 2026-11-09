using BrandWorkspace.Application.Sourcing.Commands;
using BrandWorkspace.Infrastructure.Sourcing.Services;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Infrastructure.Sourcing.CommandHandlers;

/// <summary>Handles <see cref="RouteDeleteCommand"/>.</summary>
public sealed class RouteDeleteCommandHandler(SourcingService service)
    : ICommandHandler<RouteDeleteCommand, AppResult<Guid>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<Guid>> HandleAsync(RouteDeleteCommand request, CancellationToken cancellationToken)
    {
        await service.DeleteRouteAsync(request.Access, request.RouteId, request.ExpectedRevision, cancellationToken);
        return AppResult<Guid>.Ok(request.RouteId);
    }
}
