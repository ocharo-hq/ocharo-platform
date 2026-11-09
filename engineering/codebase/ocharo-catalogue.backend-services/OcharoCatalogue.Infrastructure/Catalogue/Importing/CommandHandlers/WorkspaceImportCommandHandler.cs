using OcharoCatalogue.Application.Catalogue.Importing.Commands;
using OcharoCatalogue.Application.Catalogue.Importing.Models;
using OcharoCatalogue.Infrastructure.Catalogue.Importing.FoundationServices;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Infrastructure.Catalogue.Importing.CommandHandlers;

/// <summary>Handles <see cref="WorkspaceImportCommand"/>.</summary>
public sealed class WorkspaceImportCommandHandler(WorkspaceImportService imports)
    : ICommandHandler<WorkspaceImportCommand, AppResult<ImportReport>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<ImportReport>> HandleAsync(WorkspaceImportCommand request, CancellationToken ct)
    {
        return await imports.ImportAsync(request.Input, ct);
    }
}
