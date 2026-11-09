using BrandWorkspace.Application.Events.Queries;
using BrandWorkspace.Domain.Events.Models;
using BrandWorkspace.Infrastructure.Events.Services;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Infrastructure.Events.QueryHandlers;

/// <summary>Handles <see cref="EventSummaryGetQuery"/>.</summary>
public sealed class EventSummaryGetQueryHandler(EventService service)
    : IQueryHandler<EventSummaryGetQuery, AppResult<ShowcaseEventSummary>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<ShowcaseEventSummary>> HandleAsync(EventSummaryGetQuery request, CancellationToken cancellationToken)
    {
        return AppResult<ShowcaseEventSummary>.Ok(await service.GetSummaryAsync(request.Access, request.Today, cancellationToken));
    }
}
