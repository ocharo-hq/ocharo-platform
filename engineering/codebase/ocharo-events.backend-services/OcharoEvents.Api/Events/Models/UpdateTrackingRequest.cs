using OcharoEvents.Application.Events.Models;

namespace OcharoEvents.Api.Events.Models;

/// <summary>Represents where Ocharo stands with an event at the revision the operator edited: its stage, priority,
/// participation, cost and notes, never the event's facts.</summary>
public sealed record UpdateTrackingRequest(
    int ExpectedRevision,
    string? Status,
    string? Priority,
    string? Participation,
    string? Cost,
    string? Notes)
{
    /// <summary>Maps the request to the unvalidated tracking fields.</summary>
    public TrackingFields Fields() => new(Status, Priority, Participation, Cost, Notes);
}
