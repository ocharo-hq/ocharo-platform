using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Application.Catalogue.Publishing.Commands;

/// <summary>Represents a command to withdraw every published release of a design; it answers the design's id.</summary>
public sealed record ReleaseWithdrawCommand : ICommand<AppResult<Guid>>
{
    /// <summary>Gets the design id.</summary>
    public required Guid DesignId { get; init; }

    /// <summary>Gets the design revision the caller withdraws.</summary>
    public required int ExpectedRevision { get; init; }
}
