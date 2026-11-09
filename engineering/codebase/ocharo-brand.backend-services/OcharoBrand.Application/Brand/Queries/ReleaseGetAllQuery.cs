using OcharoBrand.Application.Brand.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoBrand.Application.Brand.Queries;

/// <summary>Represents a query to get every release with its changes, newest first.</summary>
public sealed record ReleaseGetAllQuery : IQuery<AppResult<IReadOnlyList<ReleaseSummary>>>;
