using BrandWorkspace.Application.Sourcing.Commands;
using BrandWorkspace.Application.Sourcing.Models;
using BrandWorkspace.Infrastructure.Sourcing.Services;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Infrastructure.Sourcing.CommandHandlers;

/// <summary>Handles <see cref="SourcingImportCommand"/>.</summary>
public sealed class SourcingImportCommandHandler(SourcingImportService service)
    : ICommandHandler<SourcingImportCommand, AppResult<SourcingImportReport>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<SourcingImportReport>> HandleAsync(SourcingImportCommand request, CancellationToken cancellationToken)
    {
        var report = await service.ImportAsync(request.Access, request.BundlePath, request.DryRun, cancellationToken);
        return AppResult<SourcingImportReport>.Ok(report);
    }
}
