using OcharoBrand.Application.Brand.Models;
using OcharoBrand.Application.Brand.Queries;
using OcharoBrand.Infrastructure.Brand.Extensions;
using OcharoBrand.Infrastructure.Brand.FoundationServices;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoBrand.Infrastructure.Brand.QueryHandlers;

/// <summary>Handles <see cref="PublicBrandGetQuery"/>.</summary>
public sealed class PublicBrandGetQueryHandler(PublicBrandService kit) : IQueryHandler<PublicBrandGetQuery, AppResult<PublicBrand>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<PublicBrand>> HandleAsync(PublicBrandGetQuery request, CancellationToken ct)
    {
        return (await kit.BuildAsync(request.Locale, request.Version, ct)).ToAppResult();
    }
}
