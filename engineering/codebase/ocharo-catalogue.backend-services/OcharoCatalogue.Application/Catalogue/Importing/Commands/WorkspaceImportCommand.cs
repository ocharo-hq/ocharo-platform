using OcharoCatalogue.Application.Catalogue.Importing.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Application.Catalogue.Importing.Commands;

/// <summary>Represents a command to import the workspace product records with their images.</summary>
public sealed record WorkspaceImportCommand : ICommand<AppResult<ImportReport>>
{
    /// <summary>Gets the import request.</summary>
    public required ImportInput Input { get; init; }
}
