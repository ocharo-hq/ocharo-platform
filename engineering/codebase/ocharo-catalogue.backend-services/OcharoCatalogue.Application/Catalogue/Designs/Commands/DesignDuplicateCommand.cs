using OcharoCatalogue.Application.Catalogue.Designs.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Application.Catalogue.Designs.Commands;

/// <summary>Represents a command to copy a catalogue design as an inactive design with a fresh slug.</summary>
public sealed record DesignDuplicateCommand : ICommand<AppResult<CatalogueDesignDetail>>
{
    /// <summary>Gets the design id.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets the copy's title; derived from the source when empty.</summary>
    public string? Title { get; init; }
}
