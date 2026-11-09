using OcharoBrand.Domain.Brand;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace OcharoBrand.Application.Brand.Repositories;

/// <summary>Durable brand state; draft saves are revision-checked and a release version is taken once.</summary>
public interface IBrandRepository
{
    Task<DraftRecord?> FindDraftAsync(CancellationToken ct);

    /// <summary>Saves the draft and its revision snapshot; a conflict when the stored revision is not <paramref name="expectedRevision"/>.</summary>
    Task<Result<DraftRecord>> SaveDraftAsync(DraftRecord record, int expectedRevision, string summary, CancellationToken ct);

    Task<IReadOnlyList<DraftRevisionRecord>> GetDraftRevisionsAsync(CancellationToken ct);

    Task<IReadOnlyList<LogoVersionRecord>> GetLogoVersionsAsync(CancellationToken ct);
    Task<LogoVersionRecord?> FindLogoVersionAsync(string id, CancellationToken ct);
    Task SaveLogoVersionAsync(LogoVersionRecord record, CancellationToken ct);

    Task<IReadOnlyList<ReleaseRecord>> GetReleasesAsync(CancellationToken ct);
    Task<ReleaseRecord?> FindLatestReleaseAsync(CancellationToken ct);
    Task<ReleaseRecord?> FindReleaseAsync(int version, CancellationToken ct);

    /// <summary>Inserts a release; a conflict when its version was just taken.</summary>
    Task<Result<ReleaseRecord>> InsertReleaseAsync(ReleaseRecord record, CancellationToken ct);

    Task<IReadOnlyList<AppearanceRecord>> GetAppearancesAsync(CancellationToken ct);
    Task<AppearanceRecord?> FindAppearanceAsync(string id, CancellationToken ct);
    Task SaveAppearanceAsync(AppearanceRecord record, CancellationToken ct);

    Task<MediaBlobRecord?> FindMediaAsync(string sha256, CancellationToken ct);
    Task SaveMediaAsync(MediaBlobRecord record, CancellationToken ct);
    /// <summary>The files stored before Files, which still have no file id.</summary>
    Task<IReadOnlyList<MediaBlobRecord>> GetMediaWithoutFileAsync(CancellationToken ct);
    /// <summary>Records the Files file that holds a stored file's bytes.</summary>
    Task SetMediaFileAsync(string sha256, string fileId, CancellationToken ct);

    Task<IReadOnlyList<ConsumerRecord>> GetConsumersAsync(CancellationToken ct);
    Task TouchConsumerAsync(ConsumerRecord record, CancellationToken ct);
}
