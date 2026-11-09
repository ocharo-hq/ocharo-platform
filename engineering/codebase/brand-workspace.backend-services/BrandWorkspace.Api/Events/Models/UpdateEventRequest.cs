using BrandWorkspace.Application.Events.Models;

namespace BrandWorkspace.Api.Events.Models;

/// <summary>Represents a showcase event's replacement fields at the revision the operator edited.</summary>
public sealed record UpdateEventRequest(
    int ExpectedRevision,
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
