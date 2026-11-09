using OcharoBrand.Application.Brand.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoBrand.Application.Brand.Queries;

/// <summary>Represents a query to get the draft with its logo versions and unpublished changes.</summary>
public sealed record DraftGetQuery : IQuery<AppResult<DraftView>>;
