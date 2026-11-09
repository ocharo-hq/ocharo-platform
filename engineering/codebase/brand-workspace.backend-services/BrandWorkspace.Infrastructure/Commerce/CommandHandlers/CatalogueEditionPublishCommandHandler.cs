using BrandWorkspace.Application.Commerce.Commands;
using BrandWorkspace.Application.Commerce.Repositories;
using BrandWorkspace.Application.Foundation.Errors;
using BrandWorkspace.Domain.Commerce;
using BrandWorkspace.Infrastructure.Commerce.Services;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Infrastructure.Commerce.CommandHandlers;

/// <summary>Handles <see cref="CatalogueEditionPublishCommand"/>.</summary>
public sealed class CatalogueEditionPublishCommandHandler(ICommerceRepository repository)
    : ICommandHandler<CatalogueEditionPublishCommand, AppResult<CommerceRelease>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<CommerceRelease>> HandleAsync(CatalogueEditionPublishCommand request, CancellationToken cancellationToken)
    {
        if (!request.Access.CanApprove)
            return AppResult<CommerceRelease>.Fail(DeskErrors.Forbidden("Owner approval is required."));
        var release = await repository.PublishCatalogueEditionByIdAsync(request.Access.OrganizationId, request.Access.UserId,
            request.BrandId, request.ExpectedRevision, request.EditionId, request.Note, cancellationToken);
        return AppResult<CommerceRelease>.Ok(release);
    }
}
