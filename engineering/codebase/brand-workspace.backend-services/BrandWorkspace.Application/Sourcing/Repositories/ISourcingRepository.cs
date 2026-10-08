using BrandWorkspace.Application.Sourcing.Models;
using BrandWorkspace.Domain.Sourcing.Models;

namespace BrandWorkspace.Application.Sourcing.Repositories;

/// <summary>Persists organization-scoped suppliers, their routes and people, and content-addressed media.</summary>
public interface ISourcingRepository
{
    Task<IReadOnlyList<SupplierSummary>> GetSuppliersAsync(
        Guid organizationId, string? query, string? stage, int limit, int offset, CancellationToken token);
    Task<Supplier> GetSupplierAsync(Guid organizationId, Guid supplierId, CancellationToken token);
    Task<Supplier?> FindSupplierByKeyAsync(Guid organizationId, string key, CancellationToken token);
    Task<Supplier> CreateSupplierAsync(
        Guid organizationId, string actor, string? key, SupplierInput input, CancellationToken token);
    Task<Supplier> UpdateSupplierAsync(
        Guid organizationId, Guid supplierId, int expectedRevision, SupplierInput input, CancellationToken token);

    Task<SupplierPerson> CreatePersonAsync(
        Guid organizationId, string actor, Guid supplierId, PersonInput input, CancellationToken token);
    Task<SupplierPerson> UpdatePersonAsync(
        Guid organizationId, Guid personId, int expectedRevision, PersonInput input, CancellationToken token);
    Task DeletePersonAsync(Guid organizationId, Guid personId, int expectedRevision, CancellationToken token);

    Task<ContactRoute> CreateRouteAsync(
        Guid organizationId, string actor, Guid supplierId, RouteInput input, CancellationToken token);
    Task<ContactRoute> UpdateRouteAsync(
        Guid organizationId, Guid routeId, int expectedRevision, RouteInput input, CancellationToken token);
    Task DeleteRouteAsync(Guid organizationId, Guid routeId, int expectedRevision, CancellationToken token);

    Task<IReadOnlyList<SourcingMedia>> GetMediaAsync(
        Guid organizationId, Guid? supplierId, bool unassigned, int limit, int offset, CancellationToken token);
    Task<SourcingMediaTotals> GetMediaTotalsAsync(Guid organizationId, CancellationToken token);
    Task<SourcingMedia?> FindMediaBySha256Async(Guid organizationId, string sha256, CancellationToken token);
    Task<IReadOnlyList<Guid>> FindNearDuplicatesAsync(
        Guid organizationId, long perceptualHash, int maximumDistance, Guid excludedMediaId, CancellationToken token);
    Task<SourcingMediaUpload> CreateMediaAsync(
        Guid organizationId, string actor, NewSourcingMedia media, CancellationToken token);
    Task<SourcingMedia> UpdateMediaAsync(
        Guid organizationId, Guid mediaId, int expectedRevision, MediaUpdateInput input, CancellationToken token);
    Task<int> AssignMediaAsync(
        Guid organizationId, IReadOnlyList<Guid> mediaIds, Guid? supplierId, CancellationToken token);
    Task DeleteMediaAsync(Guid organizationId, Guid mediaId, int expectedRevision, CancellationToken token);
    Task<SourcingMediaContent> GetMediaContentAsync(Guid organizationId, Guid mediaId, CancellationToken token);
    Task<byte[]> GetMediaThumbnailAsync(Guid organizationId, Guid mediaId, CancellationToken token);
}
