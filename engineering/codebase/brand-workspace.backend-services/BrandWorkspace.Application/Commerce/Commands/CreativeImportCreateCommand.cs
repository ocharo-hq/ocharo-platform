using BrandWorkspace.Application.Access.Models;
using BrandWorkspace.Domain.Commerce;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Application.Commerce.Commands;

/// <summary>Represents a command to record a Studio derivative as a creative import.</summary>
public sealed record CreativeImportCreateCommand : ICommand<AppResult<CreativeImport>>
{
    /// <summary>Gets the caller's resolved organization membership.</summary>
    public required OrganizationAccess Access { get; init; }

    /// <summary>Gets the import's manifest.</summary>
    public required CreativeImportInput Input { get; init; }
}
