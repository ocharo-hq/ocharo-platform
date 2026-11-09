using OcharoBrand.Application.Brand.Models;
using OcharoBrand.Domain.Brand;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoBrand.Application.Brand.Commands;

/// <summary>Represents a command to save the draft's identity at an expected revision.</summary>
public sealed record IdentitySaveCommand : ICommand<AppResult<DraftView>>
{
    /// <summary>Gets the revision the editor opened; a newer stored revision refuses the save.</summary>
    public required int ExpectedRevision { get; init; }

    /// <summary>Gets the edited identity, normalized before it is stored.</summary>
    public required BrandIdentity Identity { get; init; }
}
