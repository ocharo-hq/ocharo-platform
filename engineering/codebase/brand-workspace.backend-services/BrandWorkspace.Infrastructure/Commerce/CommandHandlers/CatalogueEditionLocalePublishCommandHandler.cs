using BrandWorkspace.Application.Commerce.Commands;
using BrandWorkspace.Application.Commerce.Repositories;
using BrandWorkspace.Application.Foundation.Errors;
using BrandWorkspace.Domain.Commerce;
using BrandWorkspace.Infrastructure.Commerce.Services;
using BrandWorkspace.Infrastructure.Foundation.Extensions;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Infrastructure.Commerce.CommandHandlers;

/// <summary>Handles <see cref="CatalogueEditionLocalePublishCommand"/>.</summary>
public sealed class CatalogueEditionLocalePublishCommandHandler(ICommerceRepository repository)
    : ICommandHandler<CatalogueEditionLocalePublishCommand, AppResult<CommerceRelease>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<CommerceRelease>> HandleAsync(
        CatalogueEditionLocalePublishCommand request,
        CancellationToken cancellationToken)
    {
        if (!request.Access.CanApprove)
            return AppResult<CommerceRelease>.Fail(DeskErrors.Forbidden("Owner approval is required."));
        if (!CatalogueLanguages.Supported(request.Locale))
            return AppResult<CommerceRelease>.Fail(DeskErrors.Invalid("Choose a supported catalogue language."));
        Result<CommerceRelease> release = await repository.PublishCatalogueEditionAsync(
            request.Access.OrganizationId,
            request.Access.UserId,
            request.BrandId,
            request.ExpectedRevision,
            request.Locale,
            request.Note,
            cancellationToken);
        return release.ToAppResult();
    }
}
