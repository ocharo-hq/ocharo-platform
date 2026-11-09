using BrandWorkspace.Application.Sourcing.Commands;
using BrandWorkspace.Domain.Sourcing.Models;
using BrandWorkspace.Infrastructure.Sourcing.Services;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Infrastructure.Sourcing.CommandHandlers;

/// <summary>Handles <see cref="RouteUpdateCommand"/>.</summary>
public sealed class RouteUpdateCommandHandler(SourcingService service, SourcingValidationService validation)
    : ICommandHandler<RouteUpdateCommand, AppResult<ContactRoute>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<ContactRoute>> HandleAsync(RouteUpdateCommand request, CancellationToken cancellationToken)
    {
        var input = validation.Route(request.Channel, request.Destination, request.Label, request.SourceUrl, request.Verified,
            request.IsPrimary, request.PersonId, request.Notes);
        var updated = await service.UpdateRouteAsync(request.Access, request.RouteId, request.ExpectedRevision, input, cancellationToken);
        return AppResult<ContactRoute>.Ok(updated);
    }
}
