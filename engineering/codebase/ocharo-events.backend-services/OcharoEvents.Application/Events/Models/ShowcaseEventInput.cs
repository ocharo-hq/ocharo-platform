namespace OcharoEvents.Application.Events.Models;

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
