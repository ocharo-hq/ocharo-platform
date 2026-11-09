using OcharoBrand.Application.Brand.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoBrand.Application.Brand.Queries;

/// <summary>Represents a query to get every app that read the public brand.</summary>
public sealed record ConsumerGetAllQuery : IQuery<AppResult<IReadOnlyList<ConsumerView>>>;
