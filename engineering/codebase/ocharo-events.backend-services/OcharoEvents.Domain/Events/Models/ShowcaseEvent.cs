namespace OcharoEvents.Domain.Events.Models;

/// <summary>An event where Ocharo could show its products, with its dates, terms and Ocharo's standing. The title is the
/// one in the caller's language when the event has it; <c>TitleLanguage</c> is the tag it came in (<c>uz-Latn</c>, <c>und</c>).
/// <c>EventStatus</c> says whether the event still happens as listed (<c>scheduled</c>, <c>cancelled</c>, <c>postponed</c>,
/// <c>rescheduled</c>, <c>moved-online</c>) and <c>Attendance</c> where it is attended (<c>offline</c>, <c>online</c>,
/// <c>mixed</c>); <c>Status</c> is Ocharo's own stage. <c>Tickets</c> and <c>TicketUrl</c> are what the source says attending costs;
/// <c>Cost</c> stays Ocharo's own cost of taking part. <c>Revision</c> is the tracking revision plus one; <c>EventRevision</c> is the
/// revision of the event's facts in WoW2 Events, which moves when the source revises them. <c>StartsAt</c> is the local start
/// time (<c>HH:mm</c>) when the source announced one, in <c>TimeZone</c> (an IANA zone) when it named the zone;
/// <c>Address</c> is the venue's street address and <c>OrganizerWebsite</c> the organizer's own site.</summary>
public sealed record ShowcaseEvent(
    Guid Id,
    string? Key,
    string Title,
    string? TitleLanguage,
    IReadOnlyList<string> Languages,
    string? Organizer,
    string? OrganizerWebsite,
    string Scope,
    string Format,
    string EventStatus,
    string Attendance,
    string? Country,
    string? City,
    string? Venue,
    string? Address,
    DateOnly? StartsOn,
    DateOnly? EndsOn,
    string DatePrecision,
    string? StartsAt,
    string? TimeZone,
    DateOnly? ApplicationDeadline,
    string Participation,
    string? Cost,
    ShowcaseTickets? Tickets,
    string? TicketUrl,
    string? Website,
    string? SourceUrl,
    string Status,
    string Priority,
    string Notes,
    int Revision,
    int EventRevision,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
