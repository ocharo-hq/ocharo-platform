using OcharoCatalogue.Application.Catalogue.Designs.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Application.Catalogue.Designs.Commands;

/// <summary>Represents a command to create a catalogue design.</summary>
public sealed record DesignCreateCommand : ICommand<AppResult<CatalogueDesignDetail>>
{
    /// <summary>Gets the design fields.</summary>
    public required CatalogueDesignInput Input { get; init; }
}
