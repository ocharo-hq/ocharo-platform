using OcharoCatalogue.Application.Catalogue.Core.Errors;
using OcharoCatalogue.Application.Catalogue.Core.Repositories;
using OcharoCatalogue.Application.Catalogue.Designs.Models;
using OcharoCatalogue.Application.Catalogue.Designs.Queries;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Infrastructure.Catalogue.Designs.QueryHandlers;

/// <summary>Handles <see cref="DesignRevisionGetAllQuery"/>.</summary>
public sealed class DesignRevisionGetAllQueryHandler(IDesignRepository designs)
    : IQueryHandler<DesignRevisionGetAllQuery, AppResult<IReadOnlyList<DesignRevisionSummary>>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<IReadOnlyList<DesignRevisionSummary>>> HandleAsync(
        DesignRevisionGetAllQuery request,
        CancellationToken ct)
    {
        if (await designs.GetAsync(request.Id, ct) is null)
        {
            return AppResult<IReadOnlyList<DesignRevisionSummary>>.Fail(CatalogueErrors.NotFound("Catalogue design"));
        }

        var revisions = (await designs.RevisionsAsync(request.Id, ct))
            .Select(revision => new DesignRevisionSummary(
                revision.DesignId,
                revision.Revision,
                revision.Title,
                revision.SavedAtUtc))
            .ToArray();
        return AppResult<IReadOnlyList<DesignRevisionSummary>>.Ok(revisions);
    }
}
