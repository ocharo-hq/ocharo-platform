using OcharoCatalogue.Application.Catalogue.Collections.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Application.Catalogue.Collections.Queries;

/// <summary>Represents a query to get every collection in catalogue order.</summary>
public sealed record CollectionGetAllQuery : IQuery<AppResult<IReadOnlyList<CollectionDetail>>>;
