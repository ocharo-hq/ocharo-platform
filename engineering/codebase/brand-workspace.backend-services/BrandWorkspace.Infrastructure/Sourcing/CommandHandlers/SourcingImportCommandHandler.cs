using BrandWorkspace.Application.Sourcing.Commands;
using BrandWorkspace.Application.Sourcing.Models;
using BrandWorkspace.Infrastructure.Foundation.Extensions;
using BrandWorkspace.Infrastructure.Sourcing.Services;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Infrastructure.Sourcing.CommandHandlers;

/// <summary>Handles <see cref="SourcingImportCommand"/>.</summary>
public sealed class SourcingImportCommandHandler(SourcingImportService service)
    : ICommandHandler<SourcingImportCommand, AppResult<SourcingImportReport>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<SourcingImportReport>> HandleAsync(
        SourcingImportCommand request,
        CancellationToken cancellationToken)
    {
        Result<SourcingImportReport> report = await service.ImportAsync(
            request.Access,
            request.BundlePath,
            request.DryRun,
            cancellationToken);
        return report.ToAppResult();
    }
}
