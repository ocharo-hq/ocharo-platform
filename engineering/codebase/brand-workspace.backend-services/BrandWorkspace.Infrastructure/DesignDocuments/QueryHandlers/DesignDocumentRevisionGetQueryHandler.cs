using BrandWorkspace.Application.DesignDocuments.Queries;
using BrandWorkspace.Domain.DesignDocuments.Models;
using BrandWorkspace.Infrastructure.DesignDocuments.Services;
using BrandWorkspace.Infrastructure.Foundation.Extensions;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Infrastructure.DesignDocuments.QueryHandlers;

/// <summary>Handles <see cref="DesignDocumentRevisionGetQuery"/>.</summary>
public sealed class DesignDocumentRevisionGetQueryHandler(DesignDocumentService service)
    : IQueryHandler<DesignDocumentRevisionGetQuery, AppResult<DesignDocument>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<DesignDocument>> HandleAsync(
        DesignDocumentRevisionGetQuery request,
        CancellationToken cancellationToken)
    {
        Result<DesignDocument> document = await service.GetRevisionAsync(
            request.Access,
            request.DocumentId,
            request.Revision,
            cancellationToken);
        return document.ToAppResult();
    }
}
