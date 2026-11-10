using OcharoEvents.Application.Events.Queries;
using OcharoEvents.Domain.Events.Models;
using OcharoEvents.Infrastructure.Events.Services;
using OcharoEvents.Infrastructure.Foundation.Extensions;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoEvents.Infrastructure.Events.QueryHandlers;

/// <summary>Handles <see cref="EventGetQuery"/>.</summary>
public sealed class EventGetQueryHandler(EventService service)
    : IQueryHandler<EventGetQuery, AppResult<ShowcaseEvent>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<ShowcaseEvent>> HandleAsync(
        EventGetQuery request,
        CancellationToken cancellationToken)
    {
        Result<ShowcaseEvent> found = await service.GetEventAsync(request.Access, request.EventId, cancellationToken);
        return found.ToAppResult();
    }
}
