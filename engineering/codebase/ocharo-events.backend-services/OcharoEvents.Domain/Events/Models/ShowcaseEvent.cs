namespace OcharoEvents.Domain.Events.Models;

/// <summary>An event where Ocharo could show its products, with its dates, terms and Ocharo's standing. The title is the
/// one in the caller's language when the event has it; <c>TitleLanguage</c> is the tag it came in (<c>uz-Latn</c>, <c>und</c>).
/// <c>EventStatus</c> says whether the event still happens as listed (<c>scheduled</c>, <c>cancelled</c>, <c>postponed</c>,
/// <c>rescheduled</c>, <c>moved-online</c>) and <c>Attendance</c> where it is attended (<c>offline</c>, <c>online</c>,
/// <c>mixed</c>); <c>Status</c> is Ocharo's own stage.</summary>
public sealed record ShowcaseEvent(
    Guid Id,
    string? Key,
    string Title,
    string? TitleLanguage,
    IReadOnlyList<string> Languages,
    string? Organizer,
    string Scope,
    string Format,
    string EventStatus,
    string Attendance,
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
    string Notes,
    int Revision,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
