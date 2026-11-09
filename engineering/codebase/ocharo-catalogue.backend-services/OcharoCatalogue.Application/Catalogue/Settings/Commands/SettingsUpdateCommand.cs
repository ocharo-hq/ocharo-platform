using OcharoCatalogue.Application.Catalogue.Settings.Models;
using OcharoCatalogue.Domain.Catalogue.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Application.Catalogue.Settings.Commands;

/// <summary>Represents a command to replace the catalogue settings at the revision the caller edited.</summary>
public sealed record SettingsUpdateCommand : ICommand<AppResult<CatalogueSettingsDetail>>
{
    /// <summary>Gets the new settings.</summary>
    public required CatalogueSettings Settings { get; init; }

    /// <summary>Gets the revision the caller edited; 0 before the first save.</summary>
    public required int ExpectedRevision { get; init; }
}
