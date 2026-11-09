using BrandWorkspace.Application.Access.Models;
using BrandWorkspace.Domain.Sourcing.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Application.Sourcing.Commands;

/// <summary>
/// Represents a command to replace a media item's supplier, capture and caption at an expected revision.
/// </summary>
public sealed record SourcingMediaUpdateCommand : ICommand<AppResult<SourcingMedia>>
{
    /// <summary>Gets the caller's resolved organization membership.</summary>
    public required OrganizationAccess Access { get; init; }

    /// <summary>Gets the media item to replace.</summary>
    public required Guid MediaId { get; init; }

    /// <summary>Gets the revision the operator edited.</summary>
    public required int ExpectedRevision { get; init; }

    /// <summary>Gets the supplier; none returns the item to the inbox.</summary>
    public Guid? SupplierId { get; init; }

    /// <summary>Gets how the file was captured.</summary>
    public string? Capture { get; init; }

    /// <summary>Gets the caption.</summary>
    public string? Caption { get; init; }
}
