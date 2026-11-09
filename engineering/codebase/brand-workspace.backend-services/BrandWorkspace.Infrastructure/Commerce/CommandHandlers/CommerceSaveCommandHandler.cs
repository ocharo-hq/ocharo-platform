using BrandWorkspace.Application.Commerce.Commands;
using BrandWorkspace.Application.Commerce.Repositories;
using BrandWorkspace.Application.Foundation.Errors;
using BrandWorkspace.Domain.Commerce;
using BrandWorkspace.Infrastructure.Foundation.Extensions;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Infrastructure.Commerce.CommandHandlers;

/// <summary>Handles <see cref="CommerceSaveCommand"/>.</summary>
public sealed class CommerceSaveCommandHandler(ICommerceRepository repository)
    : ICommandHandler<CommerceSaveCommand, AppResult<CommerceDraft>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<CommerceDraft>> HandleAsync(
        CommerceSaveCommand request,
        CancellationToken cancellationToken)
    {
        if (!request.Access.CanEdit)
            return AppResult<CommerceDraft>.Fail(DeskErrors.Forbidden("Editor access is required."));
        var access = request.Access;
        Result<CommerceDraft> saved = await repository.SaveAsync(
            access.OrganizationId,
            access.UserId,
            request.BrandId,
            request.ExpectedRevision,
            request.Snapshot,
            [],
            access.CanApprove,
            cancellationToken);
        return saved.ToAppResult();
    }
}
