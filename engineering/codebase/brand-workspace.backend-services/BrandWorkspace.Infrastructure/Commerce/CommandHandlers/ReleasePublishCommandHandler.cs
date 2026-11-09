using BrandWorkspace.Application.Commerce.Commands;
using BrandWorkspace.Application.Commerce.Repositories;
using BrandWorkspace.Application.Foundation.Errors;
using BrandWorkspace.Domain.Commerce;
using BrandWorkspace.Infrastructure.Commerce.Services;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Infrastructure.Commerce.CommandHandlers;

/// <summary>Handles <see cref="ReleasePublishCommand"/>.</summary>
public sealed class ReleasePublishCommandHandler(ICommerceRepository repository)
    : ICommandHandler<ReleasePublishCommand, AppResult<CommerceRelease>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<CommerceRelease>> HandleAsync(ReleasePublishCommand request, CancellationToken cancellationToken)
    {
        if (!request.Access.CanApprove)
            return AppResult<CommerceRelease>.Fail(DeskErrors.Forbidden("Owner approval is required."));
        var release = await repository.PublishAsync(request.Access.OrganizationId, request.Access.UserId, request.BrandId,
            request.ExpectedRevision, request.Note, cancellationToken);
        return AppResult<CommerceRelease>.Ok(release);
    }
}
