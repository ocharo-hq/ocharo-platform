using BrandWorkspace.Application.Commerce.Queries;
using BrandWorkspace.Application.Commerce.Repositories;
using BrandWorkspace.Application.Foundation.Errors;
using BrandWorkspace.Domain.Commerce;
using BrandWorkspace.Infrastructure.Commerce.Services;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Infrastructure.Commerce.QueryHandlers;

/// <summary>Handles <see cref="ReleaseGetAllQuery"/>.</summary>
public sealed class ReleaseGetAllQueryHandler(ICommerceRepository repository)
    : IQueryHandler<ReleaseGetAllQuery, AppResult<IReadOnlyList<CommerceRelease>>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<IReadOnlyList<CommerceRelease>>> HandleAsync(ReleaseGetAllQuery request, CancellationToken cancellationToken)
    {
        var releases = await repository.ReleasesAsync(request.Access.OrganizationId, request.BrandId, cancellationToken);
        return AppResult<IReadOnlyList<CommerceRelease>>.Ok(releases);
    }
}
