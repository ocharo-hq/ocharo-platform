using OcharoCatalogue.Application.Catalogue.Designs.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Application.Catalogue.Designs.Queries;

/// <summary>Represents a query to get what the designer composes from.</summary>
public sealed record DesignSourceGetQuery : IQuery<AppResult<DesignSource>>;
