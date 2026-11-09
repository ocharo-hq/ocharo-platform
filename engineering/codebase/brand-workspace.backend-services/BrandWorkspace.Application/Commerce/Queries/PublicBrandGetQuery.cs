using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Application.Commerce.Queries;

/// <summary>Represents a query to get a released brand's public identity.</summary>
public sealed record PublicBrandGetQuery : IQuery<AppResult<object>>
{
    /// <summary>Gets the brand's public slug.</summary>
    public required string Slug { get; init; }

    /// <summary>Gets the release to read; the latest when absent.</summary>
    public Guid? Release { get; init; }
}
