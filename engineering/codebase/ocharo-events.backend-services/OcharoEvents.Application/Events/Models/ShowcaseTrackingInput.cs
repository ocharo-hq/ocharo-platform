namespace OcharoEvents.Application.Events.Models;

/// <summary>Validated, normalized tracking fields: Ocharo's stage, priority, participation, cost and notes for an event.</summary>
public sealed record ShowcaseTrackingInput(
    string Status,
    string Priority,
    string Participation,
    string? Cost,
    string Notes);
