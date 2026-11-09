using BrandWorkspace.Application.Files.Constants;
using BrandWorkspace.Application.Files.Models;
using BrandWorkspace.Application.Files.Repositories;
using Dapper;
using WoW.Two.Sdk.Beta.Be.Data.Abstractions;

namespace BrandWorkspace.Persistence.Files.Repositories;

/// <summary>
/// Accesses Desk's rows stored before WoW2 Files and the files the migration moved for them (migration 012): the
/// immutable preparation rows get an insert-only <c>media_files</c> or <c>export_files</c> record, commerce and
/// sourcing rows their own <c>file_id</c>.
/// </summary>
public sealed class DeskFileIdRepository(IDbConnectionFactory connectionFactory) : IDeskFileIdRepository
{
    public async Task<IReadOnlyList<DeskFileWaiting>> GetWaitingAsync(CancellationToken token)
    {
        var sql =
            $"""
             SELECT '{DeskFiles.PreparationMedia}' AS record_type,
                 m.organization_id::text || '/' || m.id::text AS record_id,
                 m.sha256
             FROM preparation.media m
             WHERE m.file_id IS NULL AND NOT EXISTS (
                 SELECT 1 FROM preparation.media_files f
                 WHERE f.organization_id=m.organization_id AND f.media_id=m.id)
             UNION ALL
             SELECT '{DeskFiles.PreparationExport}',
                 e.organization_id::text || '/' || e.id::text,
                 e.sha256
             FROM preparation.exports e
             WHERE e.file_id IS NULL AND NOT EXISTS (
                 SELECT 1 FROM preparation.export_files f
                 WHERE f.organization_id=e.organization_id AND f.export_id=e.id)
             UNION ALL
             SELECT '{DeskFiles.CommerceMedia}', c.brand_id::text || '/' || c.sha256, c.sha256 FROM commerce.media c
             WHERE c.file_id IS NULL
             UNION ALL
             SELECT '{DeskFiles.SourcingMedia}', s.organization_id::text || '/' || s.sha256, s.sha256
             FROM sourcing.media_contents s
             WHERE s.file_id IS NULL
             """;
        await using var connection = await connectionFactory.CreateOpenAsync(token);
        IEnumerable<WaitingRow> rows = await connection.QueryAsync<WaitingRow>(
            new CommandDefinition(sql, cancellationToken: token));
        return rows
            .Select(row => new DeskFileWaiting(new DeskFileReference(row.RecordType, row.RecordId), row.Sha256))
            .ToList();
    }

    public async Task FillAsync(DeskFileWaiting row, string fileId, CancellationToken token)
    {
        var key = row.Reference.RecordId.Split('/', 2);
        var sql = row.Reference.RecordType switch
        {
            DeskFiles.PreparationMedia =>
                """
                INSERT INTO preparation.media_files(organization_id,media_id,file_id)
                VALUES(@owner::uuid,@record::uuid,@fileId) ON CONFLICT DO NOTHING
                """,
            DeskFiles.PreparationExport =>
                """
                INSERT INTO preparation.export_files(organization_id,export_id,file_id)
                VALUES(@owner::uuid,@record::uuid,@fileId) ON CONFLICT DO NOTHING
                """,
            DeskFiles.CommerceMedia =>
                """
                UPDATE commerce.media SET file_id=@fileId
                WHERE brand_id=@owner::uuid AND sha256=@record AND file_id IS NULL
                """,
            DeskFiles.SourcingMedia =>
                """
                UPDATE sourcing.media_contents SET file_id=@fileId
                WHERE organization_id=@owner::uuid AND sha256=@record AND file_id IS NULL
                """,
            _ => throw new ArgumentOutOfRangeException(
                nameof(row),
                row.Reference.RecordType,
                "Not a Desk file reference."),
        };
        await using var connection = await connectionFactory.CreateOpenAsync(token);
        await connection.ExecuteAsync(
            new CommandDefinition(sql, new { owner = key[0], record = key[1], fileId }, cancellationToken: token));
    }

    /// <summary>One row still waiting for its WoW2 Files id.</summary>
    private sealed class WaitingRow
    {
        public string RecordType { get; init; } = string.Empty;
        public string RecordId { get; init; } = string.Empty;
        public string Sha256 { get; init; } = string.Empty;
    }
}
