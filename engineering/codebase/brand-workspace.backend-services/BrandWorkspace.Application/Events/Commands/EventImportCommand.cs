using BrandWorkspace.Application.Access.Models;
using BrandWorkspace.Application.Events.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Application.Events.Commands;

/// <summary>Represents a command to import an events bundle as an existing editor.</summary>
public sealed record EventImportCommand : ICommand<AppResult<EventImportReport>>
{
    /// <summary>Gets the caller's resolved organization membership.</summary>
    public required OrganizationAccess Access { get; init; }

    /// <summary>Gets the bundle file's path.</summary>
    public required string BundlePath { get; init; }

    /// <summary>Gets whether the import only reports.</summary>
    public required bool DryRun { get; init; }
}
