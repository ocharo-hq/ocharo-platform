using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Application.Catalogue.Designs.Commands;

/// <summary>Represents a command to archive a catalogue design; it answers the archived design's id.</summary>
public sealed record DesignArchiveCommand : ICommand<AppResult<Guid>>
{
    /// <summary>Gets the design id.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets the revision the caller archives.</summary>
    public required int ExpectedRevision { get; init; }
}
