using BrandWorkspace.Application.Access;
using BrandWorkspace.Application.Files;
using BrandWorkspace.Application.Preparation.Models;
using BrandWorkspace.Application.Sourcing.Models;
using BrandWorkspace.Application.Sourcing.Repositories;
using BrandWorkspace.Domain.Sourcing.Models;

namespace BrandWorkspace.Infrastructure.Sourcing.Services;

/// <summary>Enforces membership roles and validation for suppliers, contacts and shared media. With WoW2 Files, an original
/// lands there once per organization and SHA-256 before its row is stored, and its 480 px variant serves as the thumbnail
/// (planning/files-adoption.md #10, #13).</summary>
public sealed class SourcingService(
    ISourcingRepository repository,
    SourcingValidationService validation,
    SourcingMediaInspector inspector,
    IDeskFileStore files)
{
    public const int NearDuplicateDistance = 6;

    public Task<IReadOnlyList<SupplierSummary>> GetSuppliersAsync(
        OrganizationAccess access, string? query, string? stage, int limit, int offset, CancellationToken token)
    {
        validation.Page(limit, offset);
        return repository.GetSuppliersAsync(
            access.OrganizationId, validation.Query(query), validation.StageFilter(stage), limit, offset, token);
    }

    public Task<Supplier> GetSupplierAsync(OrganizationAccess access, Guid supplierId, CancellationToken token) =>
        repository.GetSupplierAsync(access.OrganizationId, supplierId, token);

    public Task<Supplier> CreateSupplierAsync(OrganizationAccess access, SupplierInput input, CancellationToken token)
    {
        RequireEditor(access);
        return repository.CreateSupplierAsync(access.OrganizationId, access.UserId, null, input, token);
    }

    public Task<Supplier> UpdateSupplierAsync(
        OrganizationAccess access, Guid supplierId, int expectedRevision, SupplierInput input, CancellationToken token)
    {
        RequireEditor(access);
        validation.Revision(expectedRevision);
        return repository.UpdateSupplierAsync(access.OrganizationId, supplierId, expectedRevision, input, token);
    }

    public Task<SupplierPerson> CreatePersonAsync(
        OrganizationAccess access, Guid supplierId, PersonInput input, CancellationToken token)
    {
        RequireEditor(access);
        return repository.CreatePersonAsync(access.OrganizationId, access.UserId, supplierId, input, token);
    }

    public Task<SupplierPerson> UpdatePersonAsync(
        OrganizationAccess access, Guid personId, int expectedRevision, PersonInput input, CancellationToken token)
    {
        RequireEditor(access);
        validation.Revision(expectedRevision);
        return repository.UpdatePersonAsync(access.OrganizationId, personId, expectedRevision, input, token);
    }

    public Task DeletePersonAsync(OrganizationAccess access, Guid personId, int expectedRevision, CancellationToken token)
    {
        RequireEditor(access);
        validation.Revision(expectedRevision);
        return repository.DeletePersonAsync(access.OrganizationId, personId, expectedRevision, token);
    }

    public Task<ContactRoute> CreateRouteAsync(
        OrganizationAccess access, Guid supplierId, RouteInput input, CancellationToken token)
    {
        RequireEditor(access);
        return repository.CreateRouteAsync(access.OrganizationId, access.UserId, supplierId, input, token);
    }

    public Task<ContactRoute> UpdateRouteAsync(
        OrganizationAccess access, Guid routeId, int expectedRevision, RouteInput input, CancellationToken token)
    {
        RequireEditor(access);
        validation.Revision(expectedRevision);
        return repository.UpdateRouteAsync(access.OrganizationId, routeId, expectedRevision, input, token);
    }

    public Task DeleteRouteAsync(OrganizationAccess access, Guid routeId, int expectedRevision, CancellationToken token)
    {
        RequireEditor(access);
        validation.Revision(expectedRevision);
        return repository.DeleteRouteAsync(access.OrganizationId, routeId, expectedRevision, token);
    }

    public Task<IReadOnlyList<SourcingMedia>> GetMediaAsync(
        OrganizationAccess access, Guid? supplierId, bool unassigned, int limit, int offset, CancellationToken token)
    {
        validation.Page(limit, offset);
        if (supplierId is not null && unassigned)
            throw new PreparationException("validation", "Filter by a supplier or by the inbox, not both.");
        return repository.GetMediaAsync(access.OrganizationId, supplierId, unassigned, limit, offset, token);
    }

    public Task<SourcingMediaTotals> GetMediaTotalsAsync(OrganizationAccess access, CancellationToken token) =>
        repository.GetMediaTotalsAsync(access.OrganizationId, token);

    public async Task<SourcingMediaUpload> UploadMediaAsync(
        OrganizationAccess access, string? fileName, byte[] bytes, Guid? supplierId, string? capture, string? caption,
        CancellationToken token)
    {
        RequireEditor(access);
        var media = new NewSourcingMedia(
            supplierId,
            await inspector.InspectAsync(bytes, token),
            validation.Capture(capture),
            validation.OriginalName(fileName),
            validation.Caption(caption),
            "Desk upload",
            null,
            null);
        return await StoreAsync(access, media, token);
    }

    /// <summary>Stores inspected media once per organization and reports existing items and near duplicates.</summary>
    public async Task<SourcingMediaUpload> StoreAsync(OrganizationAccess access, NewSourcingMedia media, CancellationToken token)
    {
        RequireEditor(access);
        SourcingMedia? existing = await repository.FindMediaBySha256Async(access.OrganizationId, media.Content.Sha256, token);
        if (existing is not null) return new SourcingMediaUpload(existing, true, []);
        // A person's upload goes with their exchanged token, the import CLI with Desk's own.
        string? fileId = files.IsEnabled
            ? (await files.StoreAsync(media.Content.Bytes, DeskFiles.Name(media.Content.Sha256, media.Content.ContentType),
                media.Content.ContentType, DeskFileReference.SourcingMedia(access.OrganizationId, media.Content.Sha256), token)).Id
            : null;
        SourcingMediaUpload stored = await repository.CreateMediaAsync(access.OrganizationId, access.UserId, media, fileId, token);
        if (stored.Duplicate || media.Content.PerceptualHash is not { } hash) return stored;
        IReadOnlyList<Guid> near = await repository.FindNearDuplicatesAsync(
            access.OrganizationId, hash, NearDuplicateDistance, stored.Media.Id, token);
        return stored with { NearDuplicateIds = near };
    }

    public Task<SourcingMedia> UpdateMediaAsync(
        OrganizationAccess access, Guid mediaId, int expectedRevision, MediaUpdateInput input, CancellationToken token)
    {
        RequireEditor(access);
        validation.Revision(expectedRevision);
        return repository.UpdateMediaAsync(access.OrganizationId, mediaId, expectedRevision, input, token);
    }

    public Task<int> AssignMediaAsync(
        OrganizationAccess access, IReadOnlyList<Guid>? mediaIds, Guid? supplierId, CancellationToken token)
    {
        RequireEditor(access);
        return repository.AssignMediaAsync(access.OrganizationId, validation.MediaIds(mediaIds), supplierId, token);
    }

    public Task DeleteMediaAsync(OrganizationAccess access, Guid mediaId, int expectedRevision, CancellationToken token)
    {
        RequireEditor(access);
        validation.Revision(expectedRevision);
        return repository.DeleteMediaAsync(access.OrganizationId, mediaId, expectedRevision, token);
    }

    /// <summary>Opens an original for a route the browser loads directly (img, video, a new tab): a Files link when Files
    /// holds it, else its bytes.</summary>
    public async Task<StoredContent> OpenMediaAsync(OrganizationAccess access, Guid mediaId, CancellationToken token)
    {
        SourcingMediaContent content = await repository.GetMediaContentAsync(access.OrganizationId, mediaId, token);
        if (files.IsEnabled && content.FileId is { } fileId)
            return new StoredContent(content.ContentType, await files.LinkAsync(fileId, null, download: false, token), null);
        return new StoredContent(content.ContentType, null, content.Bytes ?? throw Unavailable());
    }

    /// <summary>Opens a thumbnail: the original's 480 px Files variant, or the WebP stored before Files.</summary>
    public async Task<StoredContent> OpenThumbnailAsync(OrganizationAccess access, Guid mediaId, CancellationToken token)
    {
        SourcingThumbnail thumbnail = await repository.GetMediaThumbnailAsync(access.OrganizationId, mediaId, token);
        if (files.IsEnabled && thumbnail.FileId is { } fileId)
            return new StoredContent("image/webp", await files.LinkAsync(fileId, DeskFiles.SourcingThumbnailWidth, download: false, token), null);
        return new StoredContent("image/webp", null, thumbnail.Bytes ?? throw Unavailable());
    }

    /// <summary>Refuses a row whose bytes only WoW2 Files holds, in a run without it.</summary>
    private static PreparationException Unavailable() =>
        new("unavailable", "This file is stored in WoW2 Files, which this run does not reach.");

    private static void RequireEditor(OrganizationAccess access)
    {
        if (!access.CanEdit) throw new PreparationException("forbidden", "An editor or owner role is required.");
    }
}
