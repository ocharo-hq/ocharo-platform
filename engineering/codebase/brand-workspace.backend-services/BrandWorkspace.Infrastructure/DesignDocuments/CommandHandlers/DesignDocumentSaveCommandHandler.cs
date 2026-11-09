using BrandWorkspace.Application.DesignDocuments.Commands;
using BrandWorkspace.Domain.DesignDocuments.Models;
using BrandWorkspace.Infrastructure.DesignDocuments.Services;
using BrandWorkspace.Infrastructure.Foundation.Extensions;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Infrastructure.DesignDocuments.CommandHandlers;

/// <summary>Handles <see cref="DesignDocumentSaveCommand"/>.</summary>
public sealed class DesignDocumentSaveCommandHandler(DesignDocumentService service)
    : ICommandHandler<DesignDocumentSaveCommand, AppResult<DesignDocument>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<DesignDocument>> HandleAsync(
        DesignDocumentSaveCommand request,
        CancellationToken cancellationToken)
    {
        Result<DesignDocument> saved = await service.SaveDocumentAsync(
            request.Access,
            request.DocumentId,
            request.ExpectedRevision,
            request.Input,
            cancellationToken);
        return saved.ToAppResult();
    }
}
