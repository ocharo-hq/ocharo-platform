using WoW.Two.Sdk.Beta.Be.Mediator;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoBrand.Application.Brand.Commands;

/// <summary>Represents a command to record that an app read the public brand.</summary>
public sealed record ConsumerTouchCommand : ICommand<AppResult<Unit>>
{
    /// <summary>Gets the app's self-declared name; a missing or malformed one records nothing.</summary>
    public string? App { get; init; }

    /// <summary>Gets the public path it read.</summary>
    public required string Path { get; init; }

    /// <summary>Gets the release it was served.</summary>
    public required int Release { get; init; }
}
