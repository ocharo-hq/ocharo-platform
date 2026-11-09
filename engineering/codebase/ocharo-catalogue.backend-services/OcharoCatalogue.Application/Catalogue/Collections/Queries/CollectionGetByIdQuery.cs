using OcharoCatalogue.Application.Catalogue.Collections.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Application.Catalogue.Collections.Queries;

/// <summary>Represents a query to get one collection.</summary>
public sealed record CollectionGetByIdQuery : IQuery<AppResult<CollectionDetail>>
{
    /// <summary>Gets the collection id.</summary>
    public required string Id { get; init; }
}
