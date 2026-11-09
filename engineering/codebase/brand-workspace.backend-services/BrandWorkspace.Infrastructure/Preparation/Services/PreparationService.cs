using BrandWorkspace.Application.Foundation.Errors;
using System.Text.RegularExpressions;
using BrandWorkspace.Application.Access.Models;
using BrandWorkspace.Application.Access.Repositories;
using BrandWorkspace.Application.Files.Constants;
using BrandWorkspace.Application.Files.Models;
using BrandWorkspace.Application.Files.Services;
using BrandWorkspace.Application.Files.Repositories;
using BrandWorkspace.Infrastructure.Files.Services;
using BrandWorkspace.Application.Preparation.Models;
using BrandWorkspace.Application.Preparation.Repositories;
using BrandWorkspace.Domain.Preparation.Models;

namespace BrandWorkspace.Infrastructure.Preparation.Services;

/// <summary>Enforces preparation permissions and bounded inputs independently of the HTTP adapter.</summary>
public sealed class PreparationService(IPreparationRepository repository, ImageValidationService images, IDeskFileStore files)
{
    public Task<IReadOnlyList<Brand>> GetBrandsAsync(OrganizationAccess access, CancellationToken token) =>
        repository.GetBrandsAsync(access.OrganizationId, token);

    public Task<Brand> CreateBrandAsync(OrganizationAccess access, string name, string timeZone, CancellationToken token)
    {
        RequireOwner(access);
        Text(name, "Brand name", 120);
        Text(timeZone, "Timezone", 100);
        if (timeZone != "UTC" && !TimeZoneInfo.TryConvertIanaIdToWindowsId(timeZone, out _))
            throw DeskErrors.Invalid("Choose a valid IANA timezone.").ToException();
        return repository.CreateBrandAsync(access.OrganizationId, name.Trim(), timeZone, token);
    }

    public Task<IReadOnlyList<MediaAsset>> GetMediaAsync(OrganizationAccess access, CancellationToken token) =>
        repository.GetMediaAsync(access.OrganizationId, token);

    public Task<MediaContent> GetMediaContentAsync(OrganizationAccess access, Guid mediaId, CancellationToken token) =>
        repository.GetMediaContentAsync(access.OrganizationId, mediaId, token);

    public Task<MediaAsset> CreateMediaAsync(OrganizationAccess access, string fileName, string provenance, byte[] bytes, CancellationToken token)
    {
        RequireEditor(access);
        Text(provenance, "Provenance", 2000);
        return repository.CreateMediaAsync(access.OrganizationId, access.UserId, fileName, provenance, images.Validate(fileName, bytes), token);
    }

    public Task<IReadOnlyList<DraftSummary>> GetDraftsAsync(OrganizationAccess access, Guid? brandId, CancellationToken token) =>
        repository.GetDraftsAsync(access.OrganizationId, brandId, token);

    public Task<Draft> GetDraftAsync(OrganizationAccess access, Guid draftId, CancellationToken token) =>
        repository.GetDraftAsync(access.OrganizationId, draftId, token);

    public Task<Draft> SaveDraftAsync(OrganizationAccess access, Guid? draftId, int? expectedRevision, DraftInput input, CancellationToken token)
    {
        RequireEditor(access);
        if (draftId is not null) Revision(expectedRevision ?? 0);
        if (input.BrandId == Guid.Empty) throw DeskErrors.Invalid("Choose a brand.").ToException();
        Text(input.Title, "Title", 200);
        Text(input.Caption, "Caption", 10000);
        Text(input.Language, "Language", 35);
        if (!Regex.IsMatch(input.Language, "^[A-Za-z]{2,8}(-[A-Za-z0-9]{1,8})*$", RegexOptions.CultureInvariant))
            throw DeskErrors.Invalid("Use a language tag such as uz, uz-Latn, or en.").ToException();
        Text(input.Destination, "Destination", 200);
        Text(input.SourceNotes, "Source notes", 4000, required: false);
        if (input.MediaIds is null || input.MediaIds.Length > 10 || input.MediaIds.Contains(Guid.Empty)
            || input.MediaIds.Distinct().Count() != input.MediaIds.Length)
            throw DeskErrors.Invalid("Choose up to 10 distinct images.").ToException();
        return repository.SaveDraftAsync(access.OrganizationId, access.UserId, draftId, expectedRevision, input, token);
    }

    public Task<Draft> ReviewAsync(OrganizationAccess access, Guid draftId, int expectedRevision, ReviewAction action, string? reason, CancellationToken token)
    {
        if (action == ReviewAction.Request) RequireEditor(access); else RequireOwner(access);
        Revision(expectedRevision);
        if (action == ReviewAction.Revoke) Text(reason, "Revocation reason", 1000);
        return repository.ReviewAsync(access.OrganizationId, access.UserId, draftId, expectedRevision, action, reason, token);
    }

    public Task<ManualExport> CreateExportAsync(OrganizationAccess access, Guid draftId, int expectedRevision, Guid idempotencyKey, CancellationToken token)
    {
        RequireEditor(access);
        Revision(expectedRevision);
        if (idempotencyKey == Guid.Empty) throw DeskErrors.Invalid("Provide an idempotency key.").ToException();
        return repository.CreateExportAsync(access.OrganizationId, access.UserId, draftId, expectedRevision, idempotencyKey, token);
    }

    public Task<byte[]> GetExportContentAsync(OrganizationAccess access, Guid exportId, CancellationToken token) =>
        repository.GetExportContentAsync(access.OrganizationId, exportId, token);

    /// <summary>Opens an export for its download route: a link to the private Files file, saved under its name, or the ZIP
    /// bytes of an export stored before Files that the backfill has not reached (planning/files-adoption.md #9, #13).</summary>
    public async Task<StoredContent> OpenExportAsync(OrganizationAccess access, Guid exportId, CancellationToken token)
    {
        if (files.IsEnabled && await repository.GetExportFileAsync(access.OrganizationId, exportId, token) is { } fileId)
            return new StoredContent("application/zip", await files.LinkAsync(fileId, null, download: true, token), null);
        return new StoredContent("application/zip", null, await repository.GetExportContentAsync(access.OrganizationId, exportId, token));
    }

    private static void Revision(int revision)
    {
        if (revision < 1 || revision == int.MaxValue) throw DeskErrors.Invalid("Provide a valid expected revision.").ToException();
    }

    private static void RequireEditor(OrganizationAccess access)
    {
        if (!access.CanEdit) throw DeskErrors.Forbidden("An editor or owner role is required.").ToException();
    }

    private static void RequireOwner(OrganizationAccess access)
    {
        if (!access.CanApprove) throw DeskErrors.Forbidden("An owner role is required.").ToException();
    }

    private static void Text(string? value, string field, int maximum, bool required = true)
    {
        if (value is null || value.Length > maximum || (required && string.IsNullOrWhiteSpace(value)) || value.Contains('\0'))
            throw DeskErrors.Invalid($"{field} must contain {(required ? "1" : "0")} to {maximum} characters.").ToException();
    }
}
