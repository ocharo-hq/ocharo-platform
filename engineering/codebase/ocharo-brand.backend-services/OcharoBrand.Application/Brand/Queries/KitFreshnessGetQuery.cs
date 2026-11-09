using OcharoBrand.Application.Brand.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoBrand.Application.Brand.Queries;

/// <summary>Represents a query to get how long the public kit stays as it is on its own.</summary>
public sealed record KitFreshnessGetQuery : IQuery<AppResult<KitFreshness>>;
