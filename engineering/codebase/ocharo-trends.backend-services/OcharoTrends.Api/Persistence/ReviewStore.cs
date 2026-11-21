using Npgsql;
using OcharoTrends.Api.Trends;

namespace OcharoTrends.Api.Persistence;

internal sealed class ReviewStore(NpgsqlDataSource dataSource)
{
    internal async Task<Dictionary<string, Review>> ListAsync(Guid organization, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("""
            SELECT video_id, status, note
            FROM trends.video_reviews
            WHERE organization_id = $1
            """);
        command.Parameters.AddWithValue(organization);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var reviews = new Dictionary<string, Review>(StringComparer.Ordinal);
        while (await reader.ReadAsync(cancellationToken))
            reviews.Add(reader.GetString(0), new Review(reader.GetString(1), reader.GetString(2)));
        return reviews;
    }

    internal async Task SaveAsync(Guid organization, string videoId, Review review, string subject, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("""
            INSERT INTO trends.video_reviews (organization_id, video_id, status, note, reviewer_subject)
            VALUES ($1, $2, $3, $4, $5)
            ON CONFLICT (organization_id, video_id) DO UPDATE
            SET status = EXCLUDED.status,
                note = EXCLUDED.note,
                reviewer_subject = EXCLUDED.reviewer_subject,
                updated_at = now()
            """);
        command.Parameters.AddWithValue(organization);
        command.Parameters.AddWithValue(videoId);
        command.Parameters.AddWithValue(review.Status);
        command.Parameters.AddWithValue(review.Note);
        command.Parameters.AddWithValue(subject);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
