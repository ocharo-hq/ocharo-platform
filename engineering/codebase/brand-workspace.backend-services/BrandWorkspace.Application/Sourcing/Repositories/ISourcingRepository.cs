using BrandWorkspace.Application.Sourcing.Models;
using BrandWorkspace.Domain.Sourcing.Models;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace BrandWorkspace.Application.Sourcing.Repositories;

/// <summary>Persists organization-scoped suppliers, their routes and people, and content-addressed media.</summary>
public interface ISourcingRepository
{
    Task<IReadOnlyList<SupplierSummary>> GetSuppliersAsync(
        Guid organizationId,
        string? query,
        string? stage,
        int limit,
        int offset,
        CancellationToken token);

    /// <summary>Reads a supplier with its people and routes; not found when the organization lacks it.</summary>
    Task<Result<Supplier>> GetSupplierAsync(Guid organizationId, Guid supplierId, CancellationToken token);

    Task<Supplier?> FindSupplierByKeyAsync(Guid organizationId, string key, CancellationToken token);

    /// <summary>Adds a supplier; a conflict when its import key is already used.</summary>
    Task<Result<Supplier>> CreateSupplierAsync(
        Guid organizationId,
        string actor,
        string? key,
        SupplierInput input,
        CancellationToken token);

    /// <summary>Changes a supplier at its expected revision; not found, or a conflict when it changed since.</summary>
    Task<Result<Supplier>> UpdateSupplierAsync(
        Guid organizationId,
        Guid supplierId,
        int expectedRevision,
        SupplierInput input,
        CancellationToken token);

    /// <summary>Adds a person to a supplier; not found when the organization lacks the supplier.</summary>
    Task<Result<SupplierPerson>> CreatePersonAsync(
        Guid organizationId,
        string actor,
        Guid supplierId,
        PersonInput input,
        CancellationToken token);

    /// <summary>Changes a person at their expected revision; not found, or a conflict when stale.</summary>
    Task<Result<SupplierPerson>> UpdatePersonAsync(
        Guid organizationId,
        Guid personId,
        int expectedRevision,
        PersonInput input,
        CancellationToken token);

    /// <summary>Removes a person at their expected revision; not found, or a conflict when stale.</summary>
    Task<Result> DeletePersonAsync(Guid organizationId, Guid personId, int expectedRevision, CancellationToken token);

    /// <summary>
    /// Adds a contact route; not found without the supplier, invalid for another supplier's person, a conflict when
    /// the route exists.
    /// </summary>
    Task<Result<ContactRoute>> CreateRouteAsync(
        Guid organizationId,
        string actor,
        Guid supplierId,
        RouteInput input,
        CancellationToken token);

    /// <summary>Changes a contact route at its expected revision; not found, invalid, or a conflict.</summary>
    Task<Result<ContactRoute>> UpdateRouteAsync(
        Guid organizationId,
        Guid routeId,
        int expectedRevision,
        RouteInput input,
        CancellationToken token);

    /// <summary>Removes a contact route at its expected revision; not found, or a conflict when stale.</summary>
    Task<Result> DeleteRouteAsync(Guid organizationId, Guid routeId, int expectedRevision, CancellationToken token);

    Task<IReadOnlyList<SourcingMedia>> GetMediaAsync(
        Guid organizationId,
        Guid? supplierId,
        bool unassigned,
        int limit,
        int offset,
        CancellationToken token);

    Task<SourcingMediaTotals> GetMediaTotalsAsync(Guid organizationId, CancellationToken token);

    Task<SourcingMedia?> FindMediaBySha256Async(Guid organizationId, string sha256, CancellationToken token);

    Task<IReadOnlyList<Guid>> FindNearDuplicatesAsync(
        Guid organizationId,
        long perceptualHash,
        int maximumDistance,
        Guid excludedMediaId,
        CancellationToken token);

    /// <summary>
    /// Stores media once per organization; with <paramref name="fileId"/> WoW2 Files holds the bytes, and the thumbnail
    /// is that file's variant, so neither stays in PostgreSQL. Invalid for a supplier outside the organization.
    /// </summary>
    Task<Result<SourcingMediaUpload>> CreateMediaAsync(
        Guid organizationId,
        string actor,
        NewSourcingMedia media,
        string? fileId,
        CancellationToken token);

    /// <summary>Changes a media item at its expected revision; invalid, not found, or a conflict.</summary>
    Task<Result<SourcingMedia>> UpdateMediaAsync(
        Guid organizationId,
        Guid mediaId,
        int expectedRevision,
        MediaUpdateInput input,
        CancellationToken token);

    /// <summary>Moves media to a supplier, or the inbox; invalid for a supplier outside the organization.</summary>
    Task<Result<int>> AssignMediaAsync(
        Guid organizationId,
        IReadOnlyList<Guid> mediaIds,
        Guid? supplierId,
        CancellationToken token);

    /// <summary>Removes a media item and its bytes at its expected revision; not found, or a conflict.</summary>
    Task<Result> DeleteMediaAsync(Guid organizationId, Guid mediaId, int expectedRevision, CancellationToken token);

    /// <summary>Reads an original's bytes or Files file; not found when the organization lacks it.</summary>
    Task<Result<SourcingMediaContent>> GetMediaContentAsync(
        Guid organizationId,
        Guid mediaId,
        CancellationToken token);

    /// <summary>Reads a thumbnail's bytes or Files file; not found when the item has none.</summary>
    Task<Result<SourcingThumbnail>> GetMediaThumbnailAsync(
        Guid organizationId,
        Guid mediaId,
        CancellationToken token);
}
