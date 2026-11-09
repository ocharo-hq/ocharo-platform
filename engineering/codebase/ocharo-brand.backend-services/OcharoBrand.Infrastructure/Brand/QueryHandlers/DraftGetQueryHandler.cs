using OcharoBrand.Application.Brand.Models;
using OcharoBrand.Application.Brand.Queries;
using OcharoBrand.Infrastructure.Brand.Extensions;
using OcharoBrand.Infrastructure.Brand.FoundationServices;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoBrand.Infrastructure.Brand.QueryHandlers;

/// <summary>Handles <see cref="DraftGetQuery"/>.</summary>
public sealed class DraftGetQueryHandler(BrandDraftService drafts) : IQueryHandler<DraftGetQuery, AppResult<DraftView>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<DraftView>> HandleAsync(DraftGetQuery request, CancellationToken ct)
    {
        return (await drafts.GetViewAsync(ct)).ToAppResult();
    }
}
