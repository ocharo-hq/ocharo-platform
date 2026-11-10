using OcharoEvents.Application.Events.Commands;
using OcharoEvents.Infrastructure.Events.Services;
using OcharoEvents.Infrastructure.Foundation.Extensions;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoEvents.Infrastructure.Events.CommandHandlers;

/// <summary>Handles <see cref="EventDeleteCommand"/>.</summary>
public sealed class EventDeleteCommandHandler(EventService service)
    : ICommandHandler<EventDeleteCommand, AppResult<Guid>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<Guid>> HandleAsync(EventDeleteCommand request, CancellationToken cancellationToken)
    {
        Result deleted = await service.DeleteEventAsync(
            request.Access,
            request.EventId,
            request.ExpectedRevision,
            cancellationToken);
        return deleted.ToAppResult(request.EventId);
    }
}
