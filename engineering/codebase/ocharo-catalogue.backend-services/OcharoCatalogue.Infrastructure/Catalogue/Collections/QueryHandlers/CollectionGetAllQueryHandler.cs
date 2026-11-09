using OcharoCatalogue.Application.Catalogue.Collections.Models;
using OcharoCatalogue.Application.Catalogue.Collections.Queries;
using OcharoCatalogue.Application.Catalogue.Core.Repositories;
using OcharoCatalogue.Infrastructure.Catalogue.Collections.Mappers;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Infrastructure.Catalogue.Collections.QueryHandlers;

/// <summary>Handles <see cref="CollectionGetAllQuery"/>.</summary>
public sealed class CollectionGetAllQueryHandler(ICatalogueRepository repository)
    : IQueryHandler<CollectionGetAllQuery, AppResult<IReadOnlyList<CollectionDetail>>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<IReadOnlyList<CollectionDetail>>> HandleAsync(
        CollectionGetAllQuery request,
        CancellationToken ct)
    {
        var collections = (await repository.GetCollectionsAsync(ct))
            .OrderBy(collection => collection.SortOrder)
            .ThenBy(collection => collection.Id, StringComparer.Ordinal)
            .Select(CollectionDetails.ToDetail)
            .ToList();
        return AppResult<IReadOnlyList<CollectionDetail>>.Ok(collections);
    }
}
