using BrandWorkspace.Application.DesignDocuments.Queries;
using BrandWorkspace.Domain.DesignDocuments.Models;
using BrandWorkspace.Infrastructure.DesignDocuments.Services;
using BrandWorkspace.Infrastructure.Foundation.Extensions;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Infrastructure.DesignDocuments.QueryHandlers;

/// <summary>Handles <see cref="DesignDocumentRevisionGetAllQuery"/>.</summary>
public sealed class DesignDocumentRevisionGetAllQueryHandler(DesignDocumentService service)
    : IQueryHandler<DesignDocumentRevisionGetAllQuery, AppResult<IReadOnlyList<DesignDocumentRevision>>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<IReadOnlyList<DesignDocumentRevision>>> HandleAsync(
        DesignDocumentRevisionGetAllQuery request,
        CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<DesignDocumentRevision>> revisions = await service.GetRevisionsAsync(
            request.Access,
            request.DocumentId,
            request.Limit,
            request.Offset,
            cancellationToken);
        return revisions.ToAppResult();
    }
}
