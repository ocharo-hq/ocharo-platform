using BrandWorkspace.Application.DesignDocuments.Queries;
using BrandWorkspace.Domain.DesignDocuments.Models;
using BrandWorkspace.Infrastructure.DesignDocuments.Services;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Infrastructure.DesignDocuments.QueryHandlers;

/// <summary>Handles <see cref="DesignDocumentGetQuery"/>.</summary>
public sealed class DesignDocumentGetQueryHandler(DesignDocumentService service)
    : IQueryHandler<DesignDocumentGetQuery, AppResult<DesignDocument>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<DesignDocument>> HandleAsync(DesignDocumentGetQuery request, CancellationToken cancellationToken)
    {
        return AppResult<DesignDocument>.Ok(await service.GetDocumentAsync(request.Access, request.DocumentId, cancellationToken));
    }
}
