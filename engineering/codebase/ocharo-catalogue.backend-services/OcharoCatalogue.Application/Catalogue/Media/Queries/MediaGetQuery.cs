using OcharoCatalogue.Application.Catalogue.Media.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Application.Catalogue.Media.Queries;

/// <summary>
/// Represents a query to open a stored image for the caller: public media to anyone, private media to members only.
/// </summary>
public sealed record MediaGetQuery : IQuery<AppResult<MediaDelivery>>
{
    /// <summary>Gets the image's SHA-256.</summary>
    public required string Sha256 { get; init; }

    /// <summary>Gets whether the web preview is wanted rather than the original.</summary>
    public required bool Preview { get; init; }
}
