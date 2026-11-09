using BrandWorkspace.Application.Foundation.Repositories;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace BrandWorkspace.Api.Foundation;

/// <summary>Reports Desk's database ready once PostgreSQL answers and Desk's schema is in place; <c>/health</c> reads it.</summary>
/// <param name="repository">The foundation repository whose readiness query decides.</param>
public sealed class SchemaReadinessHealthCheck(IFoundationRepository repository) : IHealthCheck
{
    /// <summary>Holds the title a not-ready answer carries, on <c>/health</c> and <c>api/system/status</c> alike.</summary>
    public const string NotReady = "Database schema is not ready";

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        try
        {
            return await repository.IsReadyAsync(cancellationToken)
                ? HealthCheckResult.Healthy()
                : new HealthCheckResult(context.Registration.FailureStatus, NotReady);
        }
        catch (Exception exception) when (exception is NpgsqlException or TimeoutException)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, NotReady, exception);
        }
    }
}
