using BrandWorkspace.Application.Preparation.Commands;
using BrandWorkspace.Domain.Preparation.Models;
using BrandWorkspace.Infrastructure.Preparation.Services;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Infrastructure.Preparation.CommandHandlers;

/// <summary>Handles <see cref="ExportCreateCommand"/>.</summary>
public sealed class ExportCreateCommandHandler(PreparationService service)
    : ICommandHandler<ExportCreateCommand, AppResult<ManualExport>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<ManualExport>> HandleAsync(ExportCreateCommand request, CancellationToken cancellationToken)
    {
        var export = await service.CreateExportAsync(request.Access, request.DraftId, request.ExpectedRevision, request.IdempotencyKey,
            cancellationToken);
        return AppResult<ManualExport>.Ok(export);
    }
}
