using BrandWorkspace.Application.Sourcing.Commands;
using BrandWorkspace.Domain.Sourcing.Models;
using BrandWorkspace.Infrastructure.Foundation.Extensions;
using BrandWorkspace.Infrastructure.Sourcing.Services;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Infrastructure.Sourcing.CommandHandlers;

/// <summary>Handles <see cref="SourcingMediaUploadCommand"/>.</summary>
public sealed class SourcingMediaUploadCommandHandler(SourcingService service)
    : ICommandHandler<SourcingMediaUploadCommand, AppResult<SourcingMediaUpload>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<SourcingMediaUpload>> HandleAsync(
        SourcingMediaUploadCommand request,
        CancellationToken cancellationToken)
    {
        Result<SourcingMediaUpload> upload = await service.UploadMediaAsync(
            request.Access,
            request.FileName,
            request.Bytes,
            request.SupplierId,
            request.Capture,
            request.Caption,
            cancellationToken);
        return upload.ToAppResult();
    }
}
