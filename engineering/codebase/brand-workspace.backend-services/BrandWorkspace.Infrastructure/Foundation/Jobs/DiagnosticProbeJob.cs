using BrandWorkspace.Application.Foundation.Repositories;
using Hangfire;

namespace BrandWorkspace.Infrastructure.Foundation.Jobs;

/// <summary>Writes a harmless idempotent receipt; never contacts an external provider.</summary>
public sealed class DiagnosticProbeJob(IFoundationRepository repository)
{
    /// <summary>Stores one durable receipt for this diagnostic identifier.</summary>
    [Queue("foundation")]
    [AutomaticRetry(Attempts = 3)]
    public Task ExecuteAsync(Guid probeId, CancellationToken cancellationToken) =>
        repository.RecordProbeAsync(probeId, cancellationToken);
}
