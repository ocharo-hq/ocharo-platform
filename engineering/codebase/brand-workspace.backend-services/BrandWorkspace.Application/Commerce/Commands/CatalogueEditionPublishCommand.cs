using BrandWorkspace.Application.Access.Models;
using BrandWorkspace.Domain.Commerce;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Application.Commerce.Commands;

/// <summary>Represents a command to publish one catalogue edition at an expected draft revision.</summary>
public sealed record CatalogueEditionPublishCommand : ICommand<AppResult<CommerceRelease>>
{
    /// <summary>Gets the caller's resolved organization membership.</summary>
    public required OrganizationAccess Access { get; init; }

    /// <summary>Gets the brand.</summary>
    public required Guid BrandId { get; init; }

    /// <summary>Gets the draft revision the operator saw.</summary>
    public required int ExpectedRevision { get; init; }

    /// <summary>Gets the edition to publish.</summary>
    public required Guid EditionId { get; init; }

    /// <summary>Gets the reason for publishing.</summary>
    public required string Note { get; init; }
}
