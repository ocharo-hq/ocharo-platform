using BrandWorkspace.Application.Foundation.Repositories;
using Dapper;
using WoW.Two.Sdk.Beta.Be.Data.Abstractions;

namespace BrandWorkspace.Persistence.Foundation.Repositories;

/// <summary>Accesses the technical diagnostics in the platform-owned foundation schema.</summary>
public sealed class FoundationRepository(IDbConnectionFactory connectionFactory) : IFoundationRepository
{
    /// <inheritdoc />
    public async Task<DateTimeOffset?> GetLastHeartbeatAsync(CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.CreateOpenAsync(cancellationToken);
        DateTime? lastSeen = await connection.ExecuteScalarAsync<DateTime?>(
            new CommandDefinition(
                "SELECT max(last_seen_at_utc) FROM foundation.worker_heartbeats",
                cancellationToken: cancellationToken));
        return ToTimestamp(lastSeen);
    }

    /// <inheritdoc />
    public async Task RecordHeartbeatAsync(string workerId, CancellationToken cancellationToken)
    {
        const string sql =
            """
            INSERT INTO foundation.worker_heartbeats (worker_id, last_seen_at_utc)
            VALUES (@workerId, clock_timestamp()) ON CONFLICT (worker_id)
            DO UPDATE SET last_seen_at_utc = EXCLUDED.last_seen_at_utc
            """;
        await using var connection = await connectionFactory.CreateOpenAsync(cancellationToken);
        await connection.ExecuteAsync(
            new CommandDefinition(sql, new { workerId }, cancellationToken: cancellationToken));
    }

    /// <inheritdoc />
    public async Task RecordProbeAsync(Guid probeId, CancellationToken cancellationToken)
    {
        const string sql =
            """
            INSERT INTO foundation.probe_receipts (probe_id, completed_at_utc)
            VALUES (@probeId, clock_timestamp()) ON CONFLICT (probe_id) DO NOTHING
            """;
        await using var connection = await connectionFactory.CreateOpenAsync(cancellationToken);
        await connection.ExecuteAsync(
            new CommandDefinition(sql, new { probeId }, cancellationToken: cancellationToken));
    }

    /// <inheritdoc />
    public async Task<DateTimeOffset?> GetProbeAsync(Guid probeId, CancellationToken cancellationToken)
    {
        const string sql = "SELECT completed_at_utc FROM foundation.probe_receipts WHERE probe_id = @probeId";
        await using var connection = await connectionFactory.CreateOpenAsync(cancellationToken);
        DateTime? completedAt = await connection.ExecuteScalarAsync<DateTime?>(
            new CommandDefinition(sql, new { probeId }, cancellationToken: cancellationToken));
        return ToTimestamp(completedAt);
    }

    /// <inheritdoc />
    public async Task<bool> IsReadyAsync(CancellationToken cancellationToken)
    {
        const string sql =
            """
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
            FROM foundation.schema_version WHERE id = 1
            """;
        await using var connection = await connectionFactory.CreateOpenAsync(cancellationToken);
        bool? ready = await connection.ExecuteScalarAsync<bool?>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));
        return ready is true;
    }

    /// <summary>Reads a stored timestamp as UTC.</summary>
    private static DateTimeOffset? ToTimestamp(DateTime? value)
    {
        return value is { } timestamp ? new DateTimeOffset(DateTime.SpecifyKind(timestamp, DateTimeKind.Utc)) : null;
    }
}
