using BrandWorkspace.Application.Events.Commands;
using BrandWorkspace.Domain.Events.Models;
using BrandWorkspace.Infrastructure.Events.Services;
using BrandWorkspace.Infrastructure.Foundation.Extensions;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Infrastructure.Events.CommandHandlers;

/// <summary>Handles <see cref="EventCreateCommand"/>.</summary>
public sealed class EventCreateCommandHandler(EventService service, EventValidationService validation)
    : ICommandHandler<EventCreateCommand, AppResult<ShowcaseEvent>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<ShowcaseEvent>> HandleAsync(
        EventCreateCommand request,
        CancellationToken cancellationToken)
    {
        var input = validation.Event(request.Fields);
        Result<ShowcaseEvent> created = await service.CreateEventAsync(request.Access, input, cancellationToken);
        return created.ToAppResult();
    }
}
