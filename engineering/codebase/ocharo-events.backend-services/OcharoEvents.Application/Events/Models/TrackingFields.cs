namespace OcharoEvents.Application.Events.Models;

/// <summary>Unvalidated tracking fields as the operator sent them: where Ocharo stands with an event, never its facts.</summary>
public sealed record TrackingFields(
    string? Status,
    string? Priority,
    string? Participation,
    string? Cost,
    string? Notes);
