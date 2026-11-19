using OcharoEvents.Application.Events.Models;

namespace OcharoEvents.Api.Events.Models;

/// <summary>Represents a showcase event's replacement fields at the revision the operator edited; <c>EventRevision</c> is the
/// revision of the event's facts the operator read, which lets a stale page answer 409 instead of 410.</summary>
public sealed record UpdateEventRequest(
    int ExpectedRevision,
    int? EventRevision,
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
    string? Notes)
{
    /// <summary>Maps the request to the unvalidated event fields.</summary>
    public EventFields Fields()
    {
        return new EventFields(Title, Organizer, Format, Country, City, Venue, StartsOn, EndsOn, DatePrecision,
            ApplicationDeadline, Participation, Cost, Website, SourceUrl, Status, Priority, Notes);
    }
}
