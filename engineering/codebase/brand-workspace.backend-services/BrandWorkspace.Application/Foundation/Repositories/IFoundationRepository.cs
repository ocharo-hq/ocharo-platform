namespace BrandWorkspace.Application.Foundation.Repositories;

/// <summary>Defines persistence operations for non-business foundation diagnostics.</summary>
public interface IFoundationRepository
{
    /// <summary>Reads the last heartbeat across worker instances.</summary>
    Task<DateTimeOffset?> GetLastHeartbeatAsync(CancellationToken cancellationToken);
    /// <summary>Records a worker heartbeat using database time.</summary>
    Task RecordHeartbeatAsync(string workerId, CancellationToken cancellationToken);
    /// <summary>Records an idempotent diagnostic receipt using database time.</summary>
    Task RecordProbeAsync(Guid probeId, CancellationToken cancellationToken);
    /// <summary>Reads the receipt timestamp for a harmless probe.</summary>
    Task<DateTimeOffset?> GetProbeAsync(Guid probeId, CancellationToken cancellationToken);
    /// <summary>Checks connectivity and expected application and queue schema without changing either.</summary>
    Task<bool> IsReadyAsync(CancellationToken cancellationToken);
}
