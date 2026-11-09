using BrandWorkspace.Application.Access.Models;
using BrandWorkspace.Domain.Commerce.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Application.Commerce.Commands;

/// <summary>
/// Represents a command to take a preparation image into a brand with its provenance; with WoW2 Files the brand
/// references its file.
/// </summary>
public sealed record CommerceMediaAttachCommand : ICommand<AppResult<CommerceDraft>>
{
    /// <summary>Gets the caller's resolved organization membership.</summary>
    public required OrganizationAccess Access { get; init; }

    /// <summary>Gets the brand.</summary>
    public required Guid BrandId { get; init; }

    /// <summary>Gets the draft revision the operator saw.</summary>
    public required int ExpectedRevision { get; init; }

    /// <summary>Gets the preparation image.</summary>
    public required Guid MediaId { get; init; }

    /// <summary>Gets the image's provenance.</summary>
    public required MediaApproval Provenance { get; init; }
}
