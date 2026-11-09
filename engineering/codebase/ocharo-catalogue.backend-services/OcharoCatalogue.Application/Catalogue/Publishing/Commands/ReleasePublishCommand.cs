using OcharoCatalogue.Application.Catalogue.Publishing.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Application.Catalogue.Publishing.Commands;

/// <summary>Represents a command to publish a prepared release at the design revision it was prepared from.</summary>
public sealed record ReleasePublishCommand : ICommand<AppResult<CatalogueReleaseSummary>>
{
    /// <summary>Gets the design id.</summary>
    public required Guid DesignId { get; init; }

    /// <summary>Gets the release id.</summary>
    public required Guid ReleaseId { get; init; }

    /// <summary>Gets the design revision the caller publishes.</summary>
    public required int ExpectedRevision { get; init; }
}
