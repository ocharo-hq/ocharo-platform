using OcharoBrand.Application.Brand.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoBrand.Application.Brand.Commands;

/// <summary>Represents a command to point a slot of the draft at one of its logo versions.</summary>
public sealed record LogoCurrentSetCommand : ICommand<AppResult<DraftView>>
{
    /// <summary>Gets the slot to point.</summary>
    public required string Slot { get; init; }

    /// <summary>Gets the revision the editor opened.</summary>
    public required int ExpectedRevision { get; init; }

    /// <summary>Gets the logo version the slot takes.</summary>
    public required string VersionId { get; init; }
}
