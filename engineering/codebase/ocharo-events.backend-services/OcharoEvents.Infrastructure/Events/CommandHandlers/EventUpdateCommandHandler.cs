using OcharoEvents.Application.Events.Commands;
using OcharoEvents.Domain.Events.Models;
using OcharoEvents.Infrastructure.Events.Services;
using OcharoEvents.Infrastructure.Foundation.Extensions;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoEvents.Infrastructure.Events.CommandHandlers;

/// <summary>Handles <see cref="EventUpdateCommand"/>.</summary>
public sealed class EventUpdateCommandHandler(EventService service, EventValidationService validation)
    : ICommandHandler<EventUpdateCommand, AppResult<ShowcaseEvent>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<ShowcaseEvent>> HandleAsync(
        EventUpdateCommand request,
        CancellationToken cancellationToken)
    {
        var input = validation.Event(request.Fields);
        Result<ShowcaseEvent> updated = await service.UpdateEventAsync(
            request.Access,
            request.EventId,
            request.ExpectedRevision,
            input,
            cancellationToken);
        return updated.ToAppResult();
    }
}
