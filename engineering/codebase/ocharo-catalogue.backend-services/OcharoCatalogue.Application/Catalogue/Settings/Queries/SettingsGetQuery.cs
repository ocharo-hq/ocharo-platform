using OcharoCatalogue.Application.Catalogue.Settings.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Application.Catalogue.Settings.Queries;

/// <summary>Represents a query to get the catalogue settings.</summary>
public sealed record SettingsGetQuery : IQuery<AppResult<CatalogueSettingsDetail>>;
