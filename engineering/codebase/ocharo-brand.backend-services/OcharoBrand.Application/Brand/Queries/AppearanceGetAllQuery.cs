using OcharoBrand.Domain.Brand;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoBrand.Application.Brand.Queries;

/// <summary>Represents a query to get every appearance, latest start first.</summary>
public sealed record AppearanceGetAllQuery : IQuery<AppResult<IReadOnlyList<BrandAppearance>>>;
