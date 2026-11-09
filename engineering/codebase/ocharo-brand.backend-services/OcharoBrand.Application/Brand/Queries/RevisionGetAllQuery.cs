using OcharoBrand.Application.Brand.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoBrand.Application.Brand.Queries;

/// <summary>Represents a query to get every saved draft revision, newest first.</summary>
public sealed record RevisionGetAllQuery : IQuery<AppResult<IReadOnlyList<RevisionSummary>>>;
