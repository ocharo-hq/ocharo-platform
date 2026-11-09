using OcharoBrand.Domain.Brand;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoBrand.Application.Brand.Commands;

/// <summary>Represents a command to schedule an appearance, or to change a scheduled one.</summary>
public sealed record AppearanceSaveCommand : ICommand<AppResult<BrandAppearance>>
{
    /// <summary>Gets the appearance to change; none schedules a new one.</summary>
    public string? Id { get; init; }

    /// <summary>Gets the appearance's name.</summary>
    public string? Name { get; init; }

    /// <summary>Gets when the appearance starts.</summary>
    public DateTimeOffset StartsAt { get; init; }

    /// <summary>Gets when the appearance ends.</summary>
    public DateTimeOffset EndsAt { get; init; }

    /// <summary>Gets the slot → logo version overrides during the window.</summary>
    public IReadOnlyDictionary<string, string>? Overrides { get; init; }

    /// <summary>Gets the accent colour during the window.</summary>
    public string? AccentHex { get; init; }

    /// <summary>Gets the editor's note.</summary>
    public string? Note { get; init; }

    /// <summary>Gets the event badge's label.</summary>
    public string? LinkLabel { get; init; }

    /// <summary>Gets the event badge's https destination.</summary>
    public string? LinkUrl { get; init; }
}
