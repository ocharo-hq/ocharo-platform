using BrandWorkspace.Application.Commerce.Commands;
using BrandWorkspace.Application.Commerce.Models;
using BrandWorkspace.Application.Commerce.Repositories;
using BrandWorkspace.Application.Foundation.Errors;
using BrandWorkspace.Domain.Commerce.Models;
using BrandWorkspace.Infrastructure.Commerce.Services;
using BrandWorkspace.Infrastructure.Foundation.Extensions;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Infrastructure.Commerce.CommandHandlers;

/// <summary>Handles <see cref="CommerceMediaAttachCommand"/>.</summary>
public sealed class CommerceMediaAttachCommandHandler(ICommerceRepository repository, CommerceMediaFiles media)
    : ICommandHandler<CommerceMediaAttachCommand, AppResult<CommerceDraft>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<CommerceDraft>> HandleAsync(
        CommerceMediaAttachCommand request,
        CancellationToken cancellationToken)
    {
        if (!request.Access.CanEdit)
            return AppResult<CommerceDraft>.Fail(DeskErrors.Forbidden("Editor access is required."));
        var access = request.Access;
        Result<CommerceDraftLookup> lookup = await repository.GetAsync(
            access.OrganizationId,
            request.BrandId,
            cancellationToken);
        if (lookup.IsFailure(out AppError? missing, out var found)) return AppResult<CommerceDraft>.Fail(missing);
        if (found.Draft is not { } draft)
            return AppResult<CommerceDraft>.Fail(DeskErrors.NotFound("Save a brand draft first."));
        Result<CommerceBlob> taken = await media.FromPreparationAsync(
            access.OrganizationId,
            request.BrandId,
            request.MediaId,
            cancellationToken);
        if (taken.IsFailure(out AppError? error, out var blob)) return AppResult<CommerceDraft>.Fail(error);
        var approvals = draft.Snapshot.MediaApprovals
            .ToDictionary(approval => approval.Key, approval => approval.Value);
        approvals[blob.Sha256] = request.Provenance;
        Result<CommerceDraft> saved = await repository.SaveAsync(
            access.OrganizationId,
            access.UserId,
            request.BrandId,
            request.ExpectedRevision,
            draft.Snapshot with { MediaApprovals = approvals },
            [blob],
            access.CanApprove,
            cancellationToken);
        return saved.ToAppResult();
    }
}
