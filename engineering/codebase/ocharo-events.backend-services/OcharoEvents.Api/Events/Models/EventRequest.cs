using OcharoEvents.Application.Events.Models;

namespace OcharoEvents.Api.Events.Models;

/// <summary>Represents a new showcase event as the operator sent it.</summary>
public sealed record EventRequest(
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
    string? Notes)
{
    /// <summary>Maps the request to the unvalidated event fields.</summary>
    public EventFields Fields()
    {
        return new EventFields(Title, Organizer, Scope, Format, Country, City, Venue, StartsOn, EndsOn, DatePrecision,
            ApplicationDeadline, Participation, Cost, Website, SourceUrl, Status, Priority, Notes);
    }
}
