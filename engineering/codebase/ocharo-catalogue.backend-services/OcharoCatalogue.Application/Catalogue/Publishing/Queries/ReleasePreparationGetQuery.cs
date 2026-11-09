using OcharoCatalogue.Application.Catalogue.Publishing.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Application.Catalogue.Publishing.Queries;

/// <summary>Represents a query to get what a release of a design would hold if prepared now.</summary>
public sealed record ReleasePreparationGetQuery : IQuery<AppResult<ReleasePreparation>>
{
    /// <summary>Gets the design id.</summary>
    public required Guid DesignId { get; init; }
}
