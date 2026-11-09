using OcharoCatalogue.Application.Catalogue.Core.Repositories;
using OcharoCatalogue.Application.Catalogue.Publishing.Models;
using OcharoCatalogue.Application.Catalogue.Publishing.Queries;
using OcharoCatalogue.Infrastructure.Catalogue.Publishing.Mappers;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Infrastructure.Catalogue.Publishing.QueryHandlers;

/// <summary>Handles <see cref="ReleaseGetAllQuery"/>.</summary>
public sealed class ReleaseGetAllQueryHandler(IDesignRepository designs)
    : IQueryHandler<ReleaseGetAllQuery, AppResult<IReadOnlyList<CatalogueReleaseSummary>>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<IReadOnlyList<CatalogueReleaseSummary>>> HandleAsync(
        ReleaseGetAllQuery request,
        CancellationToken ct)
    {
        var releases = (await designs.ReleasesAsync(request.DesignId, ct)).Select(ReleaseSummaries.ToSummary).ToArray();
        return AppResult<IReadOnlyList<CatalogueReleaseSummary>>.Ok(releases);
    }
}
