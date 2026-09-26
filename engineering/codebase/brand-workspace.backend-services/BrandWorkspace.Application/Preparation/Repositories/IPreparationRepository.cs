using BrandWorkspace.Application.Preparation.Models;
using BrandWorkspace.Domain.Preparation.Models;

namespace BrandWorkspace.Application.Preparation.Repositories;

/// <summary>Tenant-scoped storage with serialized draft-head transitions.</summary>
public interface IPreparationRepository
{
    Task<IReadOnlyList<Brand>> GetBrandsAsync(Guid organizationId, CancellationToken token);
    Task<Brand> CreateBrandAsync(Guid organizationId, string name, string timeZone, CancellationToken token);
    Task<IReadOnlyList<MediaAsset>> GetMediaAsync(Guid organizationId, CancellationToken token);
    Task<MediaAsset> CreateMediaAsync(Guid organizationId, string actor, string fileName, string provenance, ValidatedImage image, CancellationToken token);
    Task<MediaContent> GetMediaContentAsync(Guid organizationId, Guid mediaId, CancellationToken token);
    Task<IReadOnlyList<DraftSummary>> GetDraftsAsync(Guid organizationId, Guid? brandId, CancellationToken token);
    Task<Draft> GetDraftAsync(Guid organizationId, Guid draftId, CancellationToken token);
    Task<Draft> SaveDraftAsync(Guid organizationId, string actor, Guid? draftId, int? expectedRevision, DraftInput input, CancellationToken token);
    Task<Draft> ReviewAsync(Guid organizationId, string actor, Guid draftId, int expectedRevision, ReviewAction action, string? reason, CancellationToken token);
    Task<ManualExport> CreateExportAsync(Guid organizationId, string actor, Guid draftId, int expectedRevision, Guid idempotencyKey, CancellationToken token);
    Task<byte[]> GetExportContentAsync(Guid organizationId, Guid exportId, CancellationToken token);
}
