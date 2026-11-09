namespace BrandWorkspace.Application.Events.Models;

/// <summary>Validated list filters; undated events match a date range only when they are included.</summary>
public sealed record ShowcaseEventFilter(
    string? Query,
    string? Scope,
    string? Format,
    string? Status,
    DateOnly? From,
    DateOnly? To,
    bool IncludeUndated);
