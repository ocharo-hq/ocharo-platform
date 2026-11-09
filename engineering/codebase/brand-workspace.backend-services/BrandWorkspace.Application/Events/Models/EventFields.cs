namespace BrandWorkspace.Application.Events.Models;

/// <summary>
/// Unvalidated event fields as an operator or an import bundle sent them; dates are ISO <c>YYYY-MM-DD</c> strings.
/// </summary>
public sealed record EventFields(
    string? Title,
    string? Organizer,
    string? Scope,
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
