using BrandWorkspace.Application.Access.Models;
using BrandWorkspace.Application.Preparation.Models;
using BrandWorkspace.Domain.Preparation.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Application.Preparation.Commands;

/// <summary>Represents a command to move a draft's review state at an expected revision.</summary>
public sealed record DraftReviewCommand : ICommand<AppResult<Draft>>
{
    /// <summary>Gets the caller's resolved organization membership.</summary>
    public required OrganizationAccess Access { get; init; }

    /// <summary>Gets the draft to review.</summary>
    public required Guid DraftId { get; init; }

    /// <summary>Gets the revision the review applies to.</summary>
    public required int ExpectedRevision { get; init; }

    /// <summary>Gets the review transition.</summary>
    public required ReviewAction Action { get; init; }

    /// <summary>Gets the revocation's reason, if any.</summary>
    public string? Reason { get; init; }
}
