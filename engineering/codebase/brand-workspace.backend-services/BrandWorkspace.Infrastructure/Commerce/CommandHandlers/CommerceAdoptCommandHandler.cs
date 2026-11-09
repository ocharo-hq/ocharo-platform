using BrandWorkspace.Application.Commerce.Commands;
using BrandWorkspace.Application.Commerce.Models;
using BrandWorkspace.Application.Commerce.Repositories;
using BrandWorkspace.Application.Foundation.Errors;
using BrandWorkspace.Domain.Commerce;
using BrandWorkspace.Infrastructure.Commerce.Services;
using BrandWorkspace.Infrastructure.Foundation.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using WoW.Two.Sdk.Beta.Be.Foundation.Configuration;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Infrastructure.Commerce.CommandHandlers;

/// <summary>Handles <see cref="CommerceAdoptCommand"/>.</summary>
public sealed class CommerceAdoptCommandHandler(
    ICommerceRepository repository,
    LegacyCommerceReader reader,
    CommerceMediaFiles media,
    IConfiguration configuration,
    IHostEnvironment environment)
    : ICommandHandler<CommerceAdoptCommand, AppResult<CommerceDraft>>
{
    /// <summary>Holds the largest brand image the adoption stores.</summary>
    private const int MaximumImageBytes = 15 * 1024 * 1024;

    /// <inheritdoc />
    public async ValueTask<AppResult<CommerceDraft>> HandleAsync(
        CommerceAdoptCommand request,
        CancellationToken cancellationToken)
    {
        if (!request.Access.CanApprove)
            return AppResult<CommerceDraft>.Fail(DeskErrors.Forbidden("Owner approval is required."));
        var organizationId = request.Access.OrganizationId;
        // Global preserved data may only enter the explicitly bound organization. Check before donor access.
        if (!Guid.TryParse(configuration["Commerce:AdoptionOrganizationId"], out var adoptionOrganizationId)
            || adoptionOrganizationId == Guid.Empty || adoptionOrganizationId != organizationId)
        {
            return AppResult<CommerceDraft>.Fail(
                DeskErrors.Forbidden("Preserved source adoption is not enabled for this organization."));
        }
        Result<CommerceDraftLookup> lookup = await repository.GetAsync(
            organizationId,
            request.BrandId,
            cancellationToken);
        if (lookup.IsFailure(out AppError? missing, out var found)) return AppResult<CommerceDraft>.Fail(missing);
        if (found.Draft is not null)
        {
            return AppResult<CommerceDraft>.Fail(
                DeskErrors.Conflict("Adoption is only allowed into an empty brand draft."));
        }
        var root = configuration["Commerce:SourceRoot"];
        if (string.IsNullOrWhiteSpace(root))
        {
            if (!environment.IsLocalRun(configuration))
                return AppResult<CommerceDraft>.Fail(DeskErrors.Invalid("Preserved source storage is not configured."));
            root = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "../.."));
        }
        var imported = await reader.ReadAsync(root, request.Slug, cancellationToken);
        var blobs = await media.StoreAsync(request.BrandId, imported.Blobs, MaximumImageBytes, null, cancellationToken);
        Result<CommerceDraft> saved = await repository.SaveAsync(
            organizationId,
            request.Access.UserId,
            request.BrandId,
            0,
            imported.Snapshot,
            blobs,
            true,
            cancellationToken,
            preserveImportedPrices: true);
        return saved.ToAppResult();
    }
}
