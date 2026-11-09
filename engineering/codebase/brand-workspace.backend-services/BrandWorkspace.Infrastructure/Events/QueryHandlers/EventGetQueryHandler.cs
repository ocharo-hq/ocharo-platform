using BrandWorkspace.Application.Events.Queries;
using BrandWorkspace.Domain.Events.Models;
using BrandWorkspace.Infrastructure.Events.Services;
using BrandWorkspace.Infrastructure.Foundation.Extensions;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Infrastructure.Events.QueryHandlers;

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
