using BrandWorkspace.Application.DesignDocuments.Commands;
using BrandWorkspace.Domain.DesignDocuments.Models;
using BrandWorkspace.Infrastructure.DesignDocuments.Services;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Infrastructure.DesignDocuments.CommandHandlers;

/// <summary>Handles <see cref="DesignDocumentArchiveCommand"/>.</summary>
public sealed class DesignDocumentArchiveCommandHandler(DesignDocumentService service)
    : ICommandHandler<DesignDocumentArchiveCommand, AppResult<DesignDocument>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<DesignDocument>> HandleAsync(DesignDocumentArchiveCommand request, CancellationToken cancellationToken)
    {
        var archived = await service.ArchiveDocumentAsync(request.Access, request.DocumentId, request.ExpectedRevision, cancellationToken);
        return AppResult<DesignDocument>.Ok(archived);
    }
}
