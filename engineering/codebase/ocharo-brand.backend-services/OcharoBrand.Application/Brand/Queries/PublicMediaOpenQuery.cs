using OcharoBrand.Application.Brand.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoBrand.Application.Brand.Queries;

/// <summary>Represents a query to open a disclosed file: one of a published release, or of an appearance that has started.</summary>
public sealed record PublicMediaOpenQuery : IQuery<AppResult<MediaContent>>
{
    /// <summary>Gets the file's SHA-256.</summary>
    public required string Sha256 { get; init; }

    /// <summary>Gets whether the WebP preview is wanted instead of the original.</summary>
    public bool Preview { get; init; }
}
