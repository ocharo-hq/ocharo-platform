using OcharoCatalogue.Application.Catalogue.Publishing.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Application.Catalogue.Publishing.Queries;

/// <summary>Represents a query to get one published edition, the current one or a pinned release.</summary>
public sealed record EditionGetBySlugQuery : IQuery<AppResult<PublishedEdition>>
{
    /// <summary>Gets the catalogue slug.</summary>
    public required string Slug { get; init; }

    /// <summary>Gets the edition's language; Uzbek when null.</summary>
    public string? Locale { get; init; }

    /// <summary>Gets the pinned release; the current one when null.</summary>
    public Guid? ReleaseId { get; init; }
}
