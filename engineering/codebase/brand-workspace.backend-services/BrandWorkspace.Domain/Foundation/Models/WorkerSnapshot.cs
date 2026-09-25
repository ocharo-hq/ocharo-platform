namespace BrandWorkspace.Domain.Foundation.Models;

/// <summary>Represents the latest observed worker heartbeat, not provider delivery readiness.</summary>
public sealed record WorkerSnapshot(string State, DateTimeOffset? LastSeenAtUtc)
{
    /// <summary>Classifies a heartbeat using a 30-second freshness window.</summary>
    public static WorkerSnapshot From(DateTimeOffset? lastSeenAtUtc, DateTimeOffset now) =>
        new(lastSeenAtUtc is null ? "waiting" :
            now - lastSeenAtUtc <= TimeSpan.FromSeconds(30) && lastSeenAtUtc <= now
                ? "healthy" : "stale", lastSeenAtUtc);
}
