using OcharoCatalogue.Application.Catalogue.Publishing.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Application.Catalogue.Publishing.Commands;

/// <summary>Represents a command to prepare an immutable release of a design from its rendered artifacts.</summary>
public sealed record ReleasePrepareCommand : ICommand<AppResult<CataloguePreparedRelease>>
{
    /// <summary>Gets the design id.</summary>
    public required Guid DesignId { get; init; }

    /// <summary>Gets the revision and fingerprint sent as a JSON body; null when a form carries them.</summary>
    public ReleasePrepareMetadata? Metadata { get; init; }

    /// <summary>Gets the form's <c>metadata</c> values; null for a JSON body.</summary>
    public IReadOnlyList<string>? FormMetadata { get; init; }

    /// <summary>Gets the uploaded artifacts, in upload order.</summary>
    public IReadOnlyList<ReleaseUpload> Files { get; init; } = [];
}
