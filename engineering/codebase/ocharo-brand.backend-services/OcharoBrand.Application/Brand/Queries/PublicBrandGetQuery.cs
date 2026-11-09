using OcharoBrand.Application.Brand.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoBrand.Application.Brand.Queries;

/// <summary>Represents a query to get the public kit: the latest release with today's appearance, or a pinned release.</summary>
public sealed record PublicBrandGetQuery : IQuery<AppResult<PublicBrand>>
{
    /// <summary>Gets the language of the kit's texts (<c>uz</c>, <c>ru</c> or <c>en</c>); Uzbek otherwise.</summary>
    public string? Locale { get; init; }

    /// <summary>Gets the pinned release; none for the latest.</summary>
    public int? Version { get; init; }
}
