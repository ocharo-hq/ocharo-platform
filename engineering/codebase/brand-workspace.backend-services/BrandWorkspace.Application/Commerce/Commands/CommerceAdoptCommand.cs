using BrandWorkspace.Application.Access.Models;
using BrandWorkspace.Domain.Commerce;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Application.Commerce.Commands;

/// <summary>Represents a command to adopt a preserved brand into an empty draft of the bound organization.</summary>
public sealed record CommerceAdoptCommand : ICommand<AppResult<CommerceDraft>>
{
    /// <summary>Gets the caller's resolved organization membership.</summary>
    public required OrganizationAccess Access { get; init; }

    /// <summary>Gets the brand.</summary>
    public required Guid BrandId { get; init; }

    /// <summary>Gets the preserved brand's slug.</summary>
    public required string Slug { get; init; }
}
