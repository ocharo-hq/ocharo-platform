using BrandWorkspace.Application.Access.Models;
using BrandWorkspace.Domain.Commerce;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Application.Commerce.Commands;

/// <summary>Represents a command to attach a catalogue edition built from a saved design revision.</summary>
public sealed record CatalogueEditionAttachCommand : ICommand<AppResult<CommerceDraft>>
{
    /// <summary>Gets the caller's resolved organization membership.</summary>
    public required OrganizationAccess Access { get; init; }

    /// <summary>Gets the brand.</summary>
    public required Guid BrandId { get; init; }

    /// <summary>Gets the draft revision the operator saw.</summary>
    public required int ExpectedRevision { get; init; }

    /// <summary>Gets the saved design.</summary>
    public required Guid SourceDesignId { get; init; }

    /// <summary>Gets the design's saved revision.</summary>
    public required int SourceRevision { get; init; }

    /// <summary>Gets the uploaded artifacts by form field.</summary>
    public required IReadOnlyDictionary<string, byte[]> Files { get; init; }
}
