using BrandWorkspace.Application.Access.Models;
using BrandWorkspace.Application.Preparation.Models;
using BrandWorkspace.Domain.Preparation.Models;
using BrandWorkspace.Infrastructure.Preparation.Services;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace BrandWorkspace.Tests.E2E;

/// <summary>
/// Calls <see cref="PreparationService"/> the way the preparation tests assert on it: a value, or a failed result
/// thrown as its <see cref="WoW.Two.Sdk.Beta.Be.Foundation.Errors.AppException"/>.
/// </summary>
public sealed class PreparationServiceBridge(PreparationService service)
{
    public Task<Brand> CreateBrandAsync(
        OrganizationAccess access,
        string name,
        string timeZone,
        CancellationToken token)
    {
        return service.CreateBrandAsync(access, name, timeZone, token);
    }

    public Task<IReadOnlyList<MediaAsset>> GetMediaAsync(OrganizationAccess access, CancellationToken token)
    {
        return service.GetMediaAsync(access, token);
    }

    public async Task<MediaContent> GetMediaContentAsync(
        OrganizationAccess access,
        Guid mediaId,
        CancellationToken token)
    {
        return (await service.GetMediaContentAsync(access, mediaId, token)).ValueOrThrow();
    }

    public Task<MediaAsset> CreateMediaAsync(
        OrganizationAccess access,
        string fileName,
        string provenance,
        byte[] bytes,
        CancellationToken token)
    {
        return service.CreateMediaAsync(access, fileName, provenance, bytes, token);
    }

    public async Task<Draft> GetDraftAsync(OrganizationAccess access, Guid draftId, CancellationToken token)
    {
        return (await service.GetDraftAsync(access, draftId, token)).ValueOrThrow();
    }

    public async Task<Draft> SaveDraftAsync(
        OrganizationAccess access,
        Guid? draftId,
        int? expectedRevision,
        DraftInput input,
        CancellationToken token)
    {
        return (await service.SaveDraftAsync(access, draftId, expectedRevision, input, token)).ValueOrThrow();
    }

    public async Task<Draft> ReviewAsync(
        OrganizationAccess access,
        Guid draftId,
        int expectedRevision,
        ReviewAction action,
        string? reason,
        CancellationToken token)
    {
        return (await service.ReviewAsync(access, draftId, expectedRevision, action, reason, token)).ValueOrThrow();
    }

    public async Task<ManualExport> CreateExportAsync(
        OrganizationAccess access,
        Guid draftId,
        int expectedRevision,
        Guid idempotencyKey,
        CancellationToken token)
    {
        Result<ManualExport> export = await service.CreateExportAsync(
            access,
            draftId,
            expectedRevision,
            idempotencyKey,
            token);
        return export.ValueOrThrow();
    }

    public async Task<byte[]> GetExportContentAsync(OrganizationAccess access, Guid exportId, CancellationToken token)
    {
        return (await service.GetExportContentAsync(access, exportId, token)).ValueOrThrow();
    }
}
