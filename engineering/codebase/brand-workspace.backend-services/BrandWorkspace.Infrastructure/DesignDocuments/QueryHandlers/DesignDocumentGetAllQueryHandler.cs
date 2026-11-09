using BrandWorkspace.Application.DesignDocuments.Queries;
using BrandWorkspace.Domain.DesignDocuments.Models;
using BrandWorkspace.Infrastructure.DesignDocuments.Services;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Infrastructure.DesignDocuments.QueryHandlers;

/// <summary>Handles <see cref="DesignDocumentGetAllQuery"/>.</summary>
public sealed class DesignDocumentGetAllQueryHandler(DesignDocumentService service)
    : IQueryHandler<DesignDocumentGetAllQuery, AppResult<IReadOnlyList<DesignDocumentSummary>>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<IReadOnlyList<DesignDocumentSummary>>> HandleAsync(DesignDocumentGetAllQuery request, CancellationToken cancellationToken)
    {
        var documents = await service.GetDocumentsAsync(request.Access, request.Kind, request.IncludeArchived, request.Limit,
            request.Offset, cancellationToken);
        return AppResult<IReadOnlyList<DesignDocumentSummary>>.Ok(documents);
    }
}
