using BrandWorkspace.Application.Access.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Application.Sourcing.Commands;

/// <summary>Represents a command to move media to a supplier, or back to the inbox.</summary>
public sealed record SourcingMediaAssignCommand : ICommand<AppResult<int>>
{
    /// <summary>Gets the caller's resolved organization membership.</summary>
    public required OrganizationAccess Access { get; init; }

    /// <summary>Gets the media to move.</summary>
    public IReadOnlyList<Guid>? MediaIds { get; init; }

    /// <summary>Gets the supplier to move them to; none returns them to the inbox.</summary>
    public Guid? SupplierId { get; init; }
}
