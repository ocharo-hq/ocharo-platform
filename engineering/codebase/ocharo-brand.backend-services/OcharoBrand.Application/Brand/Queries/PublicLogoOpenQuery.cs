using OcharoBrand.Application.Brand.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoBrand.Application.Brand.Queries;

/// <summary>Represents a query to open the file a slot's stable logo address serves right now.</summary>
public sealed record PublicLogoOpenQuery : IQuery<AppResult<PublicLogoContent>>
{
    /// <summary>Gets the slot.</summary>
    public required string Slot { get; init; }

    /// <summary>Gets whether the WebP preview is wanted instead of the original.</summary>
    public bool Preview { get; init; }
}
