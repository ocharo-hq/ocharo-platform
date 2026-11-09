using BrandWorkspace.Application.Preparation.Queries;
using BrandWorkspace.Domain.Preparation.Models;
using BrandWorkspace.Infrastructure.Preparation.Services;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Infrastructure.Preparation.QueryHandlers;

/// <summary>Handles <see cref="BrandGetAllQuery"/>.</summary>
public sealed class BrandGetAllQueryHandler(PreparationService service)
    : IQueryHandler<BrandGetAllQuery, AppResult<IReadOnlyList<Brand>>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<IReadOnlyList<Brand>>> HandleAsync(
        BrandGetAllQuery request,
        CancellationToken cancellationToken)
    {
        return AppResult<IReadOnlyList<Brand>>.Ok(await service.GetBrandsAsync(request.Access, cancellationToken));
    }
}
