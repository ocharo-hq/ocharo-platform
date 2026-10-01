using BrandWorkspace.Application.Foundation.Repositories;
using Npgsql;

namespace BrandWorkspace.Persistence.Foundation.Repositories;

/// <summary>Persists technical diagnostics in the platform-owned foundation schema.</summary>
public sealed class FoundationRepository(NpgsqlDataSource dataSource) : IFoundationRepository
{
    /// <inheritdoc />
    public async Task<DateTimeOffset?> GetLastHeartbeatAsync(CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("SELECT max(last_seen_at_utc) FROM foundation.worker_heartbeats");
        return ToTimestamp(await command.ExecuteScalarAsync(cancellationToken));
    }

    /// <inheritdoc />
    public async Task RecordHeartbeatAsync(string workerId, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("""
            INSERT INTO foundation.worker_heartbeats (worker_id, last_seen_at_utc)
            VALUES ($1, clock_timestamp()) ON CONFLICT (worker_id)
            DO UPDATE SET last_seen_at_utc = EXCLUDED.last_seen_at_utc
            """);
        command.Parameters.AddWithValue(workerId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task RecordProbeAsync(Guid probeId, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("""
            INSERT INTO foundation.probe_receipts (probe_id, completed_at_utc)
            VALUES ($1, clock_timestamp()) ON CONFLICT (probe_id) DO NOTHING
            """);
        command.Parameters.AddWithValue(probeId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<DateTimeOffset?> GetProbeAsync(Guid probeId, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("SELECT completed_at_utc FROM foundation.probe_receipts WHERE probe_id = $1");
        command.Parameters.AddWithValue(probeId);
        return ToTimestamp(await command.ExecuteScalarAsync(cancellationToken));
    }

    /// <inheritdoc />
    public async Task<bool> IsReadyAsync(CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("""
            SELECT version = 1 AND to_regclass('foundation.worker_heartbeats') IS NOT NULL
                AND to_regclass('foundation.probe_receipts') IS NOT NULL
                AND to_regclass('access.users') IS NOT NULL
                AND to_regclass('access.sessions') IS NOT NULL
                AND to_regclass('access.organizations') IS NOT NULL
                AND to_regclass('access.memberships') IS NOT NULL
                AND to_regclass('preparation.brands') IS NOT NULL
                AND to_regclass('preparation.media') IS NOT NULL
                AND to_regclass('preparation.drafts') IS NOT NULL
                AND to_regclass('preparation.draft_revisions') IS NOT NULL
                AND to_regclass('preparation.draft_media') IS NOT NULL
                AND to_regclass('preparation.approvals') IS NOT NULL
                AND to_regclass('preparation.exports') IS NOT NULL
                AND to_regclass('design_documents.documents') IS NOT NULL
                AND to_regclass('design_documents.revisions') IS NOT NULL
                AND to_regclass('hangfire.job') IS NOT NULL
                AND to_regclass('hangfire.jobqueue') IS NOT NULL
            FROM foundation.schema_version WHERE id = 1
            """);
        return await command.ExecuteScalarAsync(cancellationToken) is true;
    }

    private static DateTimeOffset? ToTimestamp(object? value) => value is DateTime timestamp
        ? new DateTimeOffset(DateTime.SpecifyKind(timestamp, DateTimeKind.Utc)) : null;
}
