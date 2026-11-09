using OcharoBrand.Domain.Brand;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoBrand.Application.Brand.Commands;

/// <summary>Represents a command to mark a logo version draft or approved.</summary>
public sealed record LogoStatusSetCommand : ICommand<AppResult<LogoVersion>>
{
    /// <summary>Gets the logo version to mark.</summary>
    public required string VersionId { get; init; }

    /// <summary>Gets the status it takes.</summary>
    public required string Status { get; init; }
}
