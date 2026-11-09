using BrandWorkspace.Application.Files;
using Npgsql;

namespace BrandWorkspace.Persistence.Files;

/// <summary>Finds Desk's rows stored before WoW2 Files and records the files the migration moved for them (migration 012):
/// the immutable preparation rows get an insert-only <c>media_files</c> or <c>export_files</c> record, commerce and sourcing
/// rows their own <c>file_id</c>.</summary>
public sealed class DeskFileIdRepository(NpgsqlDataSource dataSource) : IDeskFileIdRepository
{
    public async Task<IReadOnlyList<DeskFileWaiting>> GetWaitingAsync(CancellationToken token)
    {
        await using var connection = await dataSource.OpenConnectionAsync(token);
        await using var command = new NpgsqlCommand(
            $"""
            SELECT '{DeskFiles.PreparationMedia}', m.organization_id::text || '/' || m.id::text, m.sha256 FROM preparation.media m
            WHERE m.file_id IS NULL AND NOT EXISTS (
                SELECT 1 FROM preparation.media_files f WHERE f.organization_id=m.organization_id AND f.media_id=m.id)
            UNION ALL
            SELECT '{DeskFiles.PreparationExport}', e.organization_id::text || '/' || e.id::text, e.sha256 FROM preparation.exports e
            WHERE e.file_id IS NULL AND NOT EXISTS (
                SELECT 1 FROM preparation.export_files f WHERE f.organization_id=e.organization_id AND f.export_id=e.id)
            UNION ALL
            SELECT '{DeskFiles.CommerceMedia}', c.brand_id::text || '/' || c.sha256, c.sha256 FROM commerce.media c
            WHERE c.file_id IS NULL
            UNION ALL
            SELECT '{DeskFiles.SourcingMedia}', s.organization_id::text || '/' || s.sha256, s.sha256 FROM sourcing.media_contents s
            WHERE s.file_id IS NULL
            """, connection);
        await using var reader = await command.ExecuteReaderAsync(token);
        var rows = new List<DeskFileWaiting>();
        while (await reader.ReadAsync(token))
            rows.Add(new DeskFileWaiting(new DeskFileReference(reader.GetString(0), reader.GetString(1)), reader.GetString(2)));
        return rows;
    }

    public async Task FillAsync(DeskFileWaiting row, string fileId, CancellationToken token)
    {
        var key = row.Reference.RecordId.Split('/', 2);
        var sql = row.Reference.RecordType switch
        {
            DeskFiles.PreparationMedia =>
                "INSERT INTO preparation.media_files(organization_id,media_id,file_id) VALUES($1::uuid,$2::uuid,$3) ON CONFLICT DO NOTHING",
            DeskFiles.PreparationExport =>
                "INSERT INTO preparation.export_files(organization_id,export_id,file_id) VALUES($1::uuid,$2::uuid,$3) ON CONFLICT DO NOTHING",
            DeskFiles.CommerceMedia =>
                "UPDATE commerce.media SET file_id=$3 WHERE brand_id=$1::uuid AND sha256=$2 AND file_id IS NULL",
            DeskFiles.SourcingMedia =>
                "UPDATE sourcing.media_contents SET file_id=$3 WHERE organization_id=$1::uuid AND sha256=$2 AND file_id IS NULL",
            _ => throw new ArgumentOutOfRangeException(nameof(row), row.Reference.RecordType, "Not a Desk file reference."),
        };
        await using var connection = await dataSource.OpenConnectionAsync(token);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue(key[0]);
        command.Parameters.AddWithValue(key[1]);
        command.Parameters.AddWithValue(fileId);
        await command.ExecuteNonQueryAsync(token);
    }
}
