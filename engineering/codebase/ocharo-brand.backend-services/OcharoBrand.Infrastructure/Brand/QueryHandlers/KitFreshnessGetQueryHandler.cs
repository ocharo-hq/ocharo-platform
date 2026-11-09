using OcharoBrand.Application.Brand.Models;
using OcharoBrand.Application.Brand.Queries;
using OcharoBrand.Infrastructure.Brand.FoundationServices;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoBrand.Infrastructure.Brand.QueryHandlers;

/// <summary>Handles <see cref="KitFreshnessGetQuery"/>.</summary>
public sealed class KitFreshnessGetQueryHandler(PublicBrandService kit) : IQueryHandler<KitFreshnessGetQuery, AppResult<KitFreshness>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<KitFreshness>> HandleAsync(KitFreshnessGetQuery request, CancellationToken ct)
    {
        return AppResult<KitFreshness>.Ok(new KitFreshness(await kit.SecondsToNextBoundaryAsync(ct)));
    }
}
