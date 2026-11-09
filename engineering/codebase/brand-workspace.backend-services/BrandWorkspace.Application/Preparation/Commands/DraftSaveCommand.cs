using BrandWorkspace.Application.Access.Models;
using BrandWorkspace.Application.Preparation.Models;
using BrandWorkspace.Domain.Preparation.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Application.Preparation.Commands;

/// <summary>Represents a command to create a draft, or replace one at an expected revision.</summary>
public sealed record DraftSaveCommand : ICommand<AppResult<Draft>>
{
    /// <summary>Gets the caller's resolved organization membership.</summary>
    public required OrganizationAccess Access { get; init; }

    /// <summary>Gets the draft to replace; none creates one.</summary>
    public Guid? DraftId { get; init; }

    /// <summary>Gets the revision the operator edited, when replacing.</summary>
    public int? ExpectedRevision { get; init; }

    /// <summary>Gets the draft's content.</summary>
    public required DraftInput Input { get; init; }
}
