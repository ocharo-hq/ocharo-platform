using OcharoCatalogue.Application.Catalogue.Designs.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Application.Catalogue.Designs.Queries;

/// <summary>Represents a query to get one catalogue design.</summary>
public sealed record DesignGetByIdQuery : IQuery<AppResult<CatalogueDesignDetail>>
{
    /// <summary>Gets the design id.</summary>
    public required Guid Id { get; init; }
}
