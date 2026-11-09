using OcharoBrand.Application.Brand.Queries;
using OcharoBrand.Application.Brand.Repositories;
using OcharoBrand.Domain.Brand;
using OcharoBrand.Infrastructure.Brand.Serializers;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoBrand.Infrastructure.Brand.QueryHandlers;

/// <summary>Handles <see cref="AppearanceGetAllQuery"/>.</summary>
public sealed class AppearanceGetAllQueryHandler(IBrandRepository repository)
    : IQueryHandler<AppearanceGetAllQuery, AppResult<IReadOnlyList<BrandAppearance>>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<IReadOnlyList<BrandAppearance>>> HandleAsync(AppearanceGetAllQuery request, CancellationToken ct)
    {
        var appearances = (await repository.GetAppearancesAsync(ct)).Select(r => BrandJson.Read<BrandAppearance>(r.Json))
            .OrderByDescending(a => a.StartsAt).ToList();
        return AppResult<IReadOnlyList<BrandAppearance>>.Ok(appearances);
    }
}
