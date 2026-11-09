using BrandWorkspace.Application.Events.Commands;
using BrandWorkspace.Application.Events.Models;
using BrandWorkspace.Infrastructure.Events.Services;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Infrastructure.Events.CommandHandlers;

/// <summary>Handles <see cref="EventImportCommand"/>.</summary>
public sealed class EventImportCommandHandler(EventImportService service)
    : ICommandHandler<EventImportCommand, AppResult<EventImportReport>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<EventImportReport>> HandleAsync(
        EventImportCommand request,
        CancellationToken cancellationToken)
    {
        var report = await service.ImportAsync(request.Access, request.BundlePath, request.DryRun, cancellationToken);
        return AppResult<EventImportReport>.Ok(report);
    }
}
