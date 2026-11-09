using BrandWorkspace.Application.Commerce.Queries;
using BrandWorkspace.Application.Commerce.Repositories;
using BrandWorkspace.Application.Foundation.Errors;
using BrandWorkspace.Infrastructure.Commerce.Services;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Infrastructure.Commerce.QueryHandlers;

/// <summary>Handles <see cref="PublicBrandGetQuery"/>.</summary>
public sealed class PublicBrandGetQueryHandler(ICommerceRepository repository)
    : IQueryHandler<PublicBrandGetQuery, AppResult<object>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<object>> HandleAsync(
        PublicBrandGetQuery request,
        CancellationToken cancellationToken)
    {
        if (await repository.PublicAsync(request.Slug, request.Release, cancellationToken) is not { } release)
            return AppResult<object>.Fail(DeskErrors.NotFound("The brand was not found."));
        return AppResult<object>.Ok(PublicProjection.Brand(release));
    }
}
