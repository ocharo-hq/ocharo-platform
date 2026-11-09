using BrandWorkspace.Application.Sourcing.Commands;
using BrandWorkspace.Domain.Sourcing.Models;
using BrandWorkspace.Infrastructure.Foundation.Extensions;
using BrandWorkspace.Infrastructure.Sourcing.Services;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Infrastructure.Sourcing.CommandHandlers;

/// <summary>Handles <see cref="RouteCreateCommand"/>.</summary>
public sealed class RouteCreateCommandHandler(SourcingService service, SourcingValidationService validation)
    : ICommandHandler<RouteCreateCommand, AppResult<ContactRoute>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<ContactRoute>> HandleAsync(
        RouteCreateCommand request,
        CancellationToken cancellationToken)
    {
        var input = validation.Route(
            request.Channel,
            request.Destination,
            request.Label,
            request.SourceUrl,
            request.Verified,
            request.IsPrimary,
            request.PersonId,
            request.Notes);
        Result<ContactRoute> created = await service.CreateRouteAsync(
            request.Access,
            request.SupplierId,
            input,
            cancellationToken);
        return created.ToAppResult();
    }
}
