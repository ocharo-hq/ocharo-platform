using OcharoCatalogue.Application.Catalogue.Core.Repositories;
using OcharoCatalogue.Application.Catalogue.Designs.Models;
using OcharoCatalogue.Application.Catalogue.Designs.Queries;
using OcharoCatalogue.Infrastructure.Catalogue.Designs.Mappers;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Infrastructure.Catalogue.Designs.QueryHandlers;

/// <summary>Handles <see cref="DesignGetAllQuery"/>.</summary>
public sealed class DesignGetAllQueryHandler(IDesignRepository designs)
    : IQueryHandler<DesignGetAllQuery, AppResult<IReadOnlyList<CatalogueDesignSummary>>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<IReadOnlyList<CatalogueDesignSummary>>> HandleAsync(
        DesignGetAllQuery request,
        CancellationToken ct)
    {
        var summaries = (await designs.ListAsync(request.Archived, ct)).Select(DesignProjections.ToSummary).ToArray();
        return AppResult<IReadOnlyList<CatalogueDesignSummary>>.Ok(summaries);
    }
}
