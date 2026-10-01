using System.Text.Json;
using OcharoBrand.Domain.Brand;

namespace OcharoBrand.Application.Brand;

/// <summary>A rejected brand command; the API maps it to ProblemDetails with <see cref="StatusCode"/>.</summary>
public sealed class BrandException(string message, int statusCode = 400) : Exception(message)
{
    public int StatusCode { get; } = statusCode;

    public static BrandException NotFound(string what) => new($"{what} was not found.", 404);

    public static BrandException Conflict(string detail) => new(detail, 409);
}

/// <summary>One serializer configuration for stored documents.</summary>
public static class BrandJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Write<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T Read<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, Options) ?? throw new InvalidOperationException($"Stored {typeof(T).Name} is empty.");
}

/// <summary>Durable brand state; draft saves are revision-checked (stale → 409).</summary>
public interface IBrandRepository
{
    Task<DraftRecord?> FindDraftAsync(CancellationToken ct);
    Task<DraftRecord> SaveDraftAsync(DraftRecord record, int expectedRevision, string summary, CancellationToken ct);
    Task<IReadOnlyList<DraftRevisionRecord>> GetDraftRevisionsAsync(CancellationToken ct);

    Task<IReadOnlyList<LogoVersionRecord>> GetLogoVersionsAsync(CancellationToken ct);
    Task<LogoVersionRecord?> FindLogoVersionAsync(string id, CancellationToken ct);
    Task SaveLogoVersionAsync(LogoVersionRecord record, CancellationToken ct);

    Task<IReadOnlyList<ReleaseRecord>> GetReleasesAsync(CancellationToken ct);
    Task<ReleaseRecord?> FindLatestReleaseAsync(CancellationToken ct);
    Task<ReleaseRecord?> FindReleaseAsync(int version, CancellationToken ct);
    Task<ReleaseRecord> InsertReleaseAsync(ReleaseRecord record, CancellationToken ct);

    Task<IReadOnlyList<AppearanceRecord>> GetAppearancesAsync(CancellationToken ct);
    Task<AppearanceRecord?> FindAppearanceAsync(string id, CancellationToken ct);
    Task SaveAppearanceAsync(AppearanceRecord record, CancellationToken ct);

    Task<MediaBlobRecord?> FindMediaAsync(string sha256, CancellationToken ct);
    Task SaveMediaAsync(MediaBlobRecord record, CancellationToken ct);

    Task<IReadOnlyList<ConsumerRecord>> GetConsumersAsync(CancellationToken ct);
    Task TouchConsumerAsync(ConsumerRecord record, CancellationToken ct);
}

/// <summary>Content-addressed image storage with a web preview per image.</summary>
public interface IMediaStore
{
    Task<MediaBlobRecord> StoreAsync(byte[] bytes, CancellationToken ct);
    Task<(MediaBlobRecord Record, Stream Content)?> OpenAsync(string sha256, bool preview, CancellationToken ct);
}

/// <summary>Stable and immutable media routes.</summary>
public static class BrandUrls
{
    /// <summary>Stable address that always serves the effective logo for a slot (short cache).</summary>
    public static string Logo(string slot) => $"/api/public/brand/logo/{slot}";

    /// <summary>Immutable content address of one exact file (long cache).</summary>
    public static string Media(string sha256) => $"/api/public/brand/media/{sha256}";
}
