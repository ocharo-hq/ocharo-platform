using OcharoCatalogue.Application.Catalogue.Designs.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Application.Catalogue.Designs.Commands;

/// <summary>Represents a command to save a catalogue design at the revision the caller edited.</summary>
public sealed record DesignUpdateCommand : ICommand<AppResult<CatalogueDesignDetail>>
{
    /// <summary>Gets the design id.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets the design fields.</summary>
    public required CatalogueDesignInput Input { get; init; }
}
