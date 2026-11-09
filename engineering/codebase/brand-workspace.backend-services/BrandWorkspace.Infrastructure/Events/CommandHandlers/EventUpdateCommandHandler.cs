using BrandWorkspace.Application.Events.Commands;
using BrandWorkspace.Application.Events.Models;
using BrandWorkspace.Domain.Events.Models;
using BrandWorkspace.Infrastructure.Events.Services;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Infrastructure.Events.CommandHandlers;

/// <summary>Handles <see cref="EventUpdateCommand"/>.</summary>
public sealed class EventUpdateCommandHandler(EventService service, EventValidationService validation)
    : ICommandHandler<EventUpdateCommand, AppResult<ShowcaseEvent>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<ShowcaseEvent>> HandleAsync(EventUpdateCommand request, CancellationToken cancellationToken)
    {
        var input = validation.Event(request.Fields);
        var updated = await service.UpdateEventAsync(request.Access, request.EventId, request.ExpectedRevision, input, cancellationToken);
        return AppResult<ShowcaseEvent>.Ok(updated);
    }
}
