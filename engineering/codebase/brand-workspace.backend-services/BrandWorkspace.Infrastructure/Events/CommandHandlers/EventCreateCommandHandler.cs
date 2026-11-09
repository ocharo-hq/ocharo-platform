using BrandWorkspace.Application.Events.Commands;
using BrandWorkspace.Application.Events.Models;
using BrandWorkspace.Domain.Events.Models;
using BrandWorkspace.Infrastructure.Events.Services;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Infrastructure.Events.CommandHandlers;

/// <summary>Handles <see cref="EventCreateCommand"/>.</summary>
public sealed class EventCreateCommandHandler(EventService service, EventValidationService validation)
    : ICommandHandler<EventCreateCommand, AppResult<ShowcaseEvent>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<ShowcaseEvent>> HandleAsync(EventCreateCommand request, CancellationToken cancellationToken)
    {
        var input = validation.Event(request.Fields);
        return AppResult<ShowcaseEvent>.Ok(await service.CreateEventAsync(request.Access, input, cancellationToken));
    }
}
