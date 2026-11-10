using OcharoEvents.Application.Events.Queries;
using OcharoEvents.Domain.Events.Models;
using OcharoEvents.Infrastructure.Events.Services;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoEvents.Infrastructure.Events.QueryHandlers;

/// <summary>Handles <see cref="EventSummaryGetQuery"/>.</summary>
public sealed class EventSummaryGetQueryHandler(EventService service)
    : IQueryHandler<EventSummaryGetQuery, AppResult<ShowcaseEventSummary>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<ShowcaseEventSummary>> HandleAsync(
        EventSummaryGetQuery request,
        CancellationToken cancellationToken)
    {
        return AppResult<ShowcaseEventSummary>.Ok(await service.GetSummaryAsync(
            request.Access,
            request.Today,
            cancellationToken));
    }
}
