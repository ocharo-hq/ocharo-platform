using BrandWorkspace.Application.Commerce.Queries;
using BrandWorkspace.Application.Commerce.Repositories;
using BrandWorkspace.Application.Foundation.Errors;
using BrandWorkspace.Infrastructure.Commerce.Services;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Infrastructure.Commerce.QueryHandlers;

/// <summary>Handles <see cref="PublicCatalogueGetQuery"/>.</summary>
public sealed class PublicCatalogueGetQueryHandler(ICommerceRepository repository)
    : IQueryHandler<PublicCatalogueGetQuery, AppResult<object>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<object>> HandleAsync(PublicCatalogueGetQuery request, CancellationToken cancellationToken)
    {
        if (await repository.PublicAsync(request.Slug, request.Release, cancellationToken) is not { } release)
            return AppResult<object>.Fail(DeskErrors.NotFound("The brand was not found."));
        return AppResult<object>.Ok(PublicProjection.Catalogue(release, request.Locale ?? "uz"));
    }
}
