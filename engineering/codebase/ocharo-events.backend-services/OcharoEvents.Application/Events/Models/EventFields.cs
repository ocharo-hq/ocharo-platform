namespace OcharoEvents.Application.Events.Models;

/// <summary>
/// Unvalidated event fields as an operator or an import bundle sent them; dates are ISO <c>YYYY-MM-DD</c> strings. Scope is
/// never sent: the page derives it from the country.
/// </summary>
public sealed record EventFields(
    string? Title,
    string? Organizer,
    string? Format,
    string? Country,
    string? City,
    string? Venue,
    string? StartsOn,
    string? EndsOn,
    string? DatePrecision,
    string? ApplicationDeadline,
    string? Participation,
    string? Cost,
    string? Website,
    string? SourceUrl,
    string? Status,
    string? Priority,
    string? Notes);
