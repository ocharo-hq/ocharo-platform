using BrandWorkspace.Application.Foundation.Repositories;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BrandWorkspace.Infrastructure.Foundation.BackgroundServices;

/// <summary>Records worker liveness every five seconds until shutdown.</summary>
/// <remarks>Database failures are logged and retried on the next tick; stale heartbeats remain visible.</remarks>
public sealed class WorkerHeartbeatBackgroundService(
    IFoundationRepository repository,
    ILogger<WorkerHeartbeatBackgroundService> logger) : BackgroundService
{
    private readonly string workerId = $"{Environment.MachineName}:{Guid.NewGuid():N}";

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        do
        {
            try { await repository.RecordHeartbeatAsync(workerId, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception exception) { logger.LogWarning(exception, "Worker heartbeat failed; retrying next tick"); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
