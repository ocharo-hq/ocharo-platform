using BrandWorkspace.Application.Access.Models;
using BrandWorkspace.Domain.Commerce.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Application.Commerce.Commands;

/// <summary>Represents a command to publish a brand's draft at an expected revision.</summary>
public sealed record ReleasePublishCommand : ICommand<AppResult<CommerceRelease>>
{
    /// <summary>Gets the caller's resolved organization membership.</summary>
    public required OrganizationAccess Access { get; init; }

    /// <summary>Gets the brand.</summary>
    public required Guid BrandId { get; init; }

    /// <summary>Gets the draft revision the operator saw.</summary>
    public required int ExpectedRevision { get; init; }

    /// <summary>Gets the reason for publishing.</summary>
    public required string Note { get; init; }
}
