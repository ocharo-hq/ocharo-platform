using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoBrand.Application.Brand.Queries;

/// <summary>Represents a query to get the public palette and type stacks as CSS custom properties.</summary>
public sealed record TokensCssGetQuery : IQuery<AppResult<string>>;
