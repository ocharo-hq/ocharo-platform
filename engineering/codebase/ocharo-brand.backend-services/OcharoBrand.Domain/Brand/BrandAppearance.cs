namespace OcharoBrand.Domain.Brand;

/// <summary>
/// A temporary appearance for an event (like a seasonal logo): inside its window, its slot overrides replace the
/// released logos everywhere the stable logo URLs are used. Printed or exported copies are unaffected.
/// </summary>
public sealed record BrandAppearance
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public DateTimeOffset StartsAt { get; init; }
    public DateTimeOffset EndsAt { get; init; }
    public string Status { get; init; } = AppearanceStatus.Scheduled;

    /// <summary>Slot key → logo version id used during the window.</summary>
    public IReadOnlyDictionary<string, string> Overrides { get; init; } = new Dictionary<string, string>();

    /// <summary>Optional accent colour during the window (becomes <c>--ocharo-accent</c>).</summary>
    public string? AccentHex { get; init; }

    public string? Note { get; init; }

    /// <summary>Optional event badge beside the regular logo (the logo itself keeps linking home).</summary>
    public string? LinkLabel { get; init; }

    /// <summary>Absolute https destination of the event badge.</summary>
    public string? LinkUrl { get; init; }

    public bool IsActive(DateTimeOffset at) => Status == AppearanceStatus.Scheduled && StartsAt <= at && at < EndsAt;
}
