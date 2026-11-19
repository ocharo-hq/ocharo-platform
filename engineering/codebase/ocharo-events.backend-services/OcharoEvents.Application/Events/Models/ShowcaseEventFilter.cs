namespace OcharoEvents.Application.Events.Models;

/// <summary>Validated list filters; undated events match a date range only when they are included. Without
/// <c>IncludeClosed</c>, events Ocharo skipped or rejected stay out unless <c>Status</c> names a stage.</summary>
public sealed record ShowcaseEventFilter(
    string? Query,
    string? Scope,
    string? Format,
    string? Status,
    DateOnly? From,
    DateOnly? To,
    bool IncludeUndated,
    bool IncludeClosed = true);
