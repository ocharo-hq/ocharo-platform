using OcharoCatalogue.Application.Catalogue.Publishing.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Application.Catalogue.Publishing.Queries;

/// <summary>Represents a query to get a design's prepared releases, newest first.</summary>
public sealed record ReleaseGetAllQuery : IQuery<AppResult<IReadOnlyList<CatalogueReleaseSummary>>>
{
    /// <summary>Gets the design id.</summary>
    public required Guid DesignId { get; init; }
}
