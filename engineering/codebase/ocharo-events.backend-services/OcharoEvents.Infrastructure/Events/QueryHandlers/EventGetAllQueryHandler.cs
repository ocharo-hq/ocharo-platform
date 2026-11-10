using OcharoEvents.Application.Events.Queries;
using OcharoEvents.Domain.Events.Models;
using OcharoEvents.Infrastructure.Events.Services;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoEvents.Infrastructure.Events.QueryHandlers;

/// <summary>Handles <see cref="EventGetAllQuery"/>.</summary>
public sealed class EventGetAllQueryHandler(EventService service)
    : IQueryHandler<EventGetAllQuery, AppResult<IReadOnlyList<ShowcaseEvent>>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<IReadOnlyList<ShowcaseEvent>>> HandleAsync(
        EventGetAllQuery request,
        CancellationToken cancellationToken)
    {
        var events = await service.GetEventsAsync(
            request.Access,
            request.Query,
            request.Scope,
            request.Format,
            request.Status,
            request.From,
            request.To,
            request.IncludeUndated,
            request.Limit,
            request.Offset,
            cancellationToken);
        return AppResult<IReadOnlyList<ShowcaseEvent>>.Ok(events);
    }
}
