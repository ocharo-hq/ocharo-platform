using BrandWorkspace.Application.Access.Models;
using BrandWorkspace.Domain.Preparation.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Application.Preparation.Commands;

/// <summary>Represents a command to configure a brand in the organization.</summary>
public sealed record BrandCreateCommand : ICommand<AppResult<Brand>>
{
    /// <summary>Gets the caller's resolved organization membership.</summary>
    public required OrganizationAccess Access { get; init; }

    /// <summary>Gets the brand's name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the brand's IANA timezone.</summary>
    public required string TimeZone { get; init; }
}
