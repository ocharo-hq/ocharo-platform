namespace BrandWorkspace.Application.Events.Models;

/// <summary>Validated, normalized event fields.</summary>
public sealed record ShowcaseEventInput(
    string Title,
    string? Organizer,
    string Scope,
    string Format,
    string? Country,
    string? City,
    string? Venue,
    DateOnly? StartsOn,
    DateOnly? EndsOn,
    string DatePrecision,
    DateOnly? ApplicationDeadline,
    string Participation,
    string? Cost,
    string? Website,
    string? SourceUrl,
    string Status,
    string Priority,
    string Notes);

/// <summary>Validated list filters; undated events match a date range only when they are included.</summary>
public sealed record ShowcaseEventFilter(
    string? Query,
    string? Scope,
    string? Format,
    string? Status,
    DateOnly? From,
    DateOnly? To,
    bool IncludeUndated);
