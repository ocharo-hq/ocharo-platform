using BrandWorkspace.Application.Commerce.Commands;
using BrandWorkspace.Application.Commerce.Repositories;
using BrandWorkspace.Application.Foundation.Errors;
using BrandWorkspace.Infrastructure.Commerce.Services;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Infrastructure.Commerce.CommandHandlers;

/// <summary>Handles <see cref="ReleaseWithdrawCommand"/>.</summary>
public sealed class ReleaseWithdrawCommandHandler(ICommerceRepository repository)
    : ICommandHandler<ReleaseWithdrawCommand, AppResult<Guid>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<Guid>> HandleAsync(ReleaseWithdrawCommand request, CancellationToken cancellationToken)
    {
        if (!request.Access.CanApprove)
            return AppResult<Guid>.Fail(DeskErrors.Forbidden("Owner approval is required."));
        await repository.WithdrawAsync(request.Access.OrganizationId, request.Access.UserId, request.BrandId, request.ReleaseId,
            request.Reason, cancellationToken);
        return AppResult<Guid>.Ok(request.ReleaseId);
    }
}
