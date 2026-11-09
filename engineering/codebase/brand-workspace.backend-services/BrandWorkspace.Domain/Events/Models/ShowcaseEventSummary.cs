namespace BrandWorkspace.Domain.Events.Models;

/// <summary>Counts per status, every status present, and the nearest open application deadlines.</summary>
public sealed record ShowcaseEventSummary(
    IReadOnlyDictionary<string, int> ByStatus,
    IReadOnlyList<ShowcaseEvent> UpcomingDeadlines);
