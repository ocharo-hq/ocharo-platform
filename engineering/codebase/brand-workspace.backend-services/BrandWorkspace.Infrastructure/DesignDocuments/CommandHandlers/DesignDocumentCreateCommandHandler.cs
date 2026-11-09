using BrandWorkspace.Application.DesignDocuments.Commands;
using BrandWorkspace.Domain.DesignDocuments.Models;
using BrandWorkspace.Infrastructure.DesignDocuments.Services;
using BrandWorkspace.Infrastructure.Foundation.Extensions;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Infrastructure.DesignDocuments.CommandHandlers;

/// <summary>Handles <see cref="DesignDocumentCreateCommand"/>.</summary>
public sealed class DesignDocumentCreateCommandHandler(DesignDocumentService service)
    : ICommandHandler<DesignDocumentCreateCommand, AppResult<DesignDocument>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<DesignDocument>> HandleAsync(
        DesignDocumentCreateCommand request,
        CancellationToken cancellationToken)
    {
        Result<DesignDocument> created = await service.CreateDocumentAsync(
            request.Access,
            request.Kind,
            request.Input,
            cancellationToken);
        return created.ToAppResult();
    }
}
