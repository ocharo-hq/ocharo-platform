using OcharoCatalogue.Application.Catalogue.Collections.Models;
using OcharoCatalogue.Application.Catalogue.Collections.Queries;
using OcharoCatalogue.Application.Catalogue.Core.Errors;
using OcharoCatalogue.Application.Catalogue.Core.Repositories;
using OcharoCatalogue.Infrastructure.Catalogue.Collections.Mappers;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Infrastructure.Catalogue.Collections.QueryHandlers;

/// <summary>Handles <see cref="CollectionGetByIdQuery"/>.</summary>
public sealed class CollectionGetByIdQueryHandler(ICatalogueRepository repository)
    : IQueryHandler<CollectionGetByIdQuery, AppResult<CollectionDetail>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<CollectionDetail>> HandleAsync(
        CollectionGetByIdQuery request,
        CancellationToken ct)
    {
        var record = await repository.FindCollectionAsync(request.Id, ct);
        if (record is null)
        {
            return AppResult<CollectionDetail>.Fail(CatalogueErrors.NotFound("Collection"));
        }

        return AppResult<CollectionDetail>.Ok(CollectionDetails.ToDetail(record));
    }
}
