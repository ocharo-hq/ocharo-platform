using OcharoEvents.Application.Events.Commands;
using OcharoEvents.Domain.Events.Models;
using OcharoEvents.Infrastructure.Events.Services;
using OcharoEvents.Infrastructure.Foundation.Extensions;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoEvents.Infrastructure.Events.CommandHandlers;

/// <summary>Handles <see cref="EventTrackingUpdateCommand"/>.</summary>
public sealed class EventTrackingUpdateCommandHandler(EventService service, EventValidationService validation)
    : ICommandHandler<EventTrackingUpdateCommand, AppResult<ShowcaseEvent>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<ShowcaseEvent>> HandleAsync(
        EventTrackingUpdateCommand request,
        CancellationToken cancellationToken)
    {
        var input = validation.Tracking(request.Fields);
        Result<ShowcaseEvent> updated = await service.UpdateTrackingAsync(
            request.Access,
            request.EventId,
            request.ExpectedRevision,
            input,
            cancellationToken);
        return updated.ToAppResult();
    }
}
