using BrandWorkspace.Application.Access.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Application.Access.Commands;

/// <summary>Represents a command to project a signed-in member and their organization into Desk, so authored records keep their foreign keys.</summary>
public sealed record MemberProjectCommand : ICommand<AppResult<OrganizationAccess>>
{
    /// <summary>Gets the caller's resolved organization membership.</summary>
    public required OrganizationAccess Access { get; init; }

    /// <summary>Gets the organization's name as Identity names it.</summary>
    public string? OrganizationName { get; init; }

    /// <summary>Gets the organization's slug as Identity names it.</summary>
    public string? OrganizationSlug { get; init; }

    /// <summary>Gets the person's display name.</summary>
    public string? DisplayName { get; init; }

    /// <summary>Gets the person's email.</summary>
    public string? Email { get; init; }
}
