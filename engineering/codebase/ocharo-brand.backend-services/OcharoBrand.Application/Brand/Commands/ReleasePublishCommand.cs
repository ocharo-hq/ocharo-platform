using OcharoBrand.Application.Brand.Models;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoBrand.Application.Brand.Commands;

/// <summary>Represents a command to publish the draft as the next release.</summary>
public sealed record ReleasePublishCommand : ICommand<AppResult<ReleaseSummary>>
{
    /// <summary>Gets the revision the approver reviewed.</summary>
    public required int ExpectedRevision { get; init; }

    /// <summary>Gets the release note; the changes are listed when none is given.</summary>
    public string? Note { get; init; }
}
