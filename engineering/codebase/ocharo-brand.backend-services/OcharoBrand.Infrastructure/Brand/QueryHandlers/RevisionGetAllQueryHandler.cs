using OcharoBrand.Application.Brand.Models;
using OcharoBrand.Application.Brand.Queries;
using OcharoBrand.Application.Brand.Repositories;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoBrand.Infrastructure.Brand.QueryHandlers;

/// <summary>Handles <see cref="RevisionGetAllQuery"/>.</summary>
public sealed class RevisionGetAllQueryHandler(IBrandRepository repository)
    : IQueryHandler<RevisionGetAllQuery, AppResult<IReadOnlyList<RevisionSummary>>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<IReadOnlyList<RevisionSummary>>> HandleAsync(RevisionGetAllQuery request, CancellationToken ct)
    {
        var revisions = (await repository.GetDraftRevisionsAsync(ct)).OrderByDescending(r => r.Revision)
            .Select(r => new RevisionSummary(r.Revision, r.SavedAt, r.Summary)).ToList();
        return AppResult<IReadOnlyList<RevisionSummary>>.Ok(revisions);
    }
}
