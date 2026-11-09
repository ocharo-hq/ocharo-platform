using BrandWorkspace.Application.Preparation.Models;
using BrandWorkspace.Domain.Preparation.Models;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace BrandWorkspace.Application.Preparation.Repositories;

/// <summary>Tenant-scoped storage with serialized draft-head transitions.</summary>
public interface IPreparationRepository
{
    Task<IReadOnlyList<Brand>> GetBrandsAsync(Guid organizationId, CancellationToken token);

    Task<Brand> CreateBrandAsync(Guid organizationId, string name, string timeZone, CancellationToken token);

    Task<IReadOnlyList<MediaAsset>> GetMediaAsync(Guid organizationId, CancellationToken token);

    Task<MediaAsset> CreateMediaAsync(
        Guid organizationId,
        string actor,
        string fileName,
        string provenance,
        ValidatedImage image,
        CancellationToken token);

    /// <summary>Reads an image and its exact bytes; not found when the organization lacks it.</summary>
    Task<Result<MediaContent>> GetMediaContentAsync(Guid organizationId, Guid mediaId, CancellationToken token);

    /// <summary>
    /// Reads an image's metadata and the WoW2 Files file holding its bytes, none while PostgreSQL alone holds them;
    /// not found when the organization lacks it.
    /// </summary>
    Task<Result<PreparationMediaFile>> GetMediaFileAsync(Guid organizationId, Guid mediaId, CancellationToken token);

    /// <summary>Lists the drafts, of one brand when named; not found when the organization lacks that brand.</summary>
    Task<Result<IReadOnlyList<DraftSummary>>> GetDraftsAsync(
        Guid organizationId,
        Guid? brandId,
        CancellationToken token);

    /// <summary>Reads a draft at its current revision; not found when the organization lacks it.</summary>
    Task<Result<Draft>> GetDraftAsync(Guid organizationId, Guid draftId, CancellationToken token);

    /// <summary>
    /// Creates a draft, or saves its next revision at the expected one; not found, too large, or a conflict when it
    /// changed since.
    /// </summary>
    Task<Result<Draft>> SaveDraftAsync(
        Guid organizationId,
        string actor,
        Guid? draftId,
        int? expectedRevision,
        DraftInput input,
        CancellationToken token);

    /// <summary>Requests review of, approves or revokes the expected revision; not found, or a conflict.</summary>
    Task<Result<Draft>> ReviewAsync(
        Guid organizationId,
        string actor,
        Guid draftId,
        int expectedRevision,
        ReviewAction action,
        string? reason,
        CancellationToken token);

    /// <summary>
    /// Exports the approved revision once per idempotency key; not found, or a conflict when the revision changed, is
    /// unapproved, or the key was used for different content.
    /// </summary>
    Task<Result<ManualExport>> CreateExportAsync(
        Guid organizationId,
        string actor,
        Guid draftId,
        int expectedRevision,
        Guid idempotencyKey,
        CancellationToken token);

    /// <summary>Reads an export's ZIP bytes; not found when the organization lacks it.</summary>
    Task<Result<byte[]>> GetExportContentAsync(Guid organizationId, Guid exportId, CancellationToken token);

    /// <summary>
    /// Reads the WoW2 Files file holding an export's ZIP, none while PostgreSQL alone holds it; not found when the
    /// organization lacks the export.
    /// </summary>
    Task<Result<ExportFileLookup>> GetExportFileAsync(Guid organizationId, Guid exportId, CancellationToken token);
}
