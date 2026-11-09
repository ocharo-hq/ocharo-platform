using OcharoCatalogue.Application.Catalogue.Designs.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Application.Catalogue.Designs.Queries;

/// <summary>Represents a query to get a design as it was at one saved revision.</summary>
public sealed record DesignRevisionGetByIdQuery : IQuery<AppResult<CatalogueDesignDetail>>
{
    /// <summary>Gets the design id.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets the revision number.</summary>
    public required int Revision { get; init; }
}
