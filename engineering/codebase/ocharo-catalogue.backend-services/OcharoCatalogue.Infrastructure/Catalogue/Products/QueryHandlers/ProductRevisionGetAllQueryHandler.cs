using OcharoCatalogue.Application.Catalogue.Core.Errors;
using OcharoCatalogue.Application.Catalogue.Core.Repositories;
using OcharoCatalogue.Application.Catalogue.Products.Models;
using OcharoCatalogue.Application.Catalogue.Products.Queries;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Infrastructure.Catalogue.Products.QueryHandlers;

/// <summary>Handles <see cref="ProductRevisionGetAllQuery"/>.</summary>
public sealed class ProductRevisionGetAllQueryHandler(ICatalogueRepository repository)
    : IQueryHandler<ProductRevisionGetAllQuery, AppResult<IReadOnlyList<RevisionSummary>>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<IReadOnlyList<RevisionSummary>>> HandleAsync(
        ProductRevisionGetAllQuery request,
        CancellationToken ct)
    {
        if (await repository.FindProductAsync(request.Id, ct) is null)
        {
            return AppResult<IReadOnlyList<RevisionSummary>>.Fail(CatalogueErrors.NotFound("Product"));
        }

        var revisions = (await repository.GetRevisionsAsync(request.Id, ct))
            .OrderByDescending(revision => revision.Revision)
            .Select(revision => new RevisionSummary(
                revision.Revision,
                revision.SavedAt,
                revision.Summary))
            .ToList();
        return AppResult<IReadOnlyList<RevisionSummary>>.Ok(revisions);
    }
}
