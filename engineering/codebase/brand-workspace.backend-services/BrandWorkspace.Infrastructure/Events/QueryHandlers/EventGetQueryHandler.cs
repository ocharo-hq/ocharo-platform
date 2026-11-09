using BrandWorkspace.Application.Events.Queries;
using BrandWorkspace.Domain.Events.Models;
using BrandWorkspace.Infrastructure.Events.Services;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Infrastructure.Events.QueryHandlers;

/// <summary>Handles <see cref="EventGetQuery"/>.</summary>
public sealed class EventGetQueryHandler(EventService service)
    : IQueryHandler<EventGetQuery, AppResult<ShowcaseEvent>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<ShowcaseEvent>> HandleAsync(EventGetQuery request, CancellationToken cancellationToken)
    {
        return AppResult<ShowcaseEvent>.Ok(await service.GetEventAsync(request.Access, request.EventId, cancellationToken));
    }
}
