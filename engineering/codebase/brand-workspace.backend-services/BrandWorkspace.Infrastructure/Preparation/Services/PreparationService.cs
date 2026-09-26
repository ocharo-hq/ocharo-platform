using System.Text.RegularExpressions;
using BrandWorkspace.Application.Access;
using BrandWorkspace.Application.Preparation.Models;
using BrandWorkspace.Application.Preparation.Repositories;
using BrandWorkspace.Domain.Preparation.Models;

namespace BrandWorkspace.Infrastructure.Preparation.Services;

/// <summary>Enforces preparation permissions and bounded inputs independently of the HTTP adapter.</summary>
public sealed class PreparationService(IPreparationRepository repository, ImageValidationService images)
{
    public Task<IReadOnlyList<Brand>> GetBrandsAsync(OrganizationAccess access, CancellationToken token) =>
        repository.GetBrandsAsync(access.OrganizationId, token);

    public Task<Brand> CreateBrandAsync(OrganizationAccess access, string name, string timeZone, CancellationToken token)
    {
        RequireOwner(access);
        Text(name, "Brand name", 120);
        Text(timeZone, "Timezone", 100);
        if (timeZone != "UTC" && !TimeZoneInfo.TryConvertIanaIdToWindowsId(timeZone, out _))
            throw new PreparationException("validation", "Choose a valid IANA timezone.");
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
        if (input.BrandId == Guid.Empty) throw new PreparationException("validation", "Choose a brand.");
        Text(input.Title, "Title", 200);
        Text(input.Caption, "Caption", 10000);
        Text(input.Language, "Language", 35);
        if (!Regex.IsMatch(input.Language, "^[A-Za-z]{2,8}(-[A-Za-z0-9]{1,8})*$", RegexOptions.CultureInvariant))
            throw new PreparationException("validation", "Use a language tag such as uz, uz-Latn, or en.");
        Text(input.Destination, "Destination", 200);
        Text(input.SourceNotes, "Source notes", 4000, required: false);
        if (input.MediaIds is null || input.MediaIds.Length > 10 || input.MediaIds.Contains(Guid.Empty)
            || input.MediaIds.Distinct().Count() != input.MediaIds.Length)
            throw new PreparationException("validation", "Choose up to 10 distinct images.");
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
        if (idempotencyKey == Guid.Empty) throw new PreparationException("validation", "Provide an idempotency key.");
        return repository.CreateExportAsync(access.OrganizationId, access.UserId, draftId, expectedRevision, idempotencyKey, token);
    }

    public Task<byte[]> GetExportContentAsync(OrganizationAccess access, Guid exportId, CancellationToken token) =>
        repository.GetExportContentAsync(access.OrganizationId, exportId, token);

    private static void Revision(int revision)
    {
        if (revision < 1 || revision == int.MaxValue) throw new PreparationException("validation", "Provide a valid expected revision.");
    }

    private static void RequireEditor(OrganizationAccess access)
    {
        if (!access.CanEdit) throw new PreparationException("forbidden", "An editor or owner role is required.");
    }

    private static void RequireOwner(OrganizationAccess access)
    {
        if (!access.CanApprove) throw new PreparationException("forbidden", "An owner role is required.");
    }

    private static void Text(string? value, string field, int maximum, bool required = true)
    {
        if (value is null || value.Length > maximum || (required && string.IsNullOrWhiteSpace(value)) || value.Contains('\0'))
            throw new PreparationException("validation", $"{field} must contain {(required ? "1" : "0")} to {maximum} characters.");
    }
}
