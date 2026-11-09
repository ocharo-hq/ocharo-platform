using BrandWorkspace.Application.Preparation.Commands;
using BrandWorkspace.Domain.Preparation.Models;
using BrandWorkspace.Infrastructure.Preparation.Services;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Infrastructure.Preparation.CommandHandlers;

/// <summary>Handles <see cref="PreparationMediaUploadCommand"/>.</summary>
public sealed class PreparationMediaUploadCommandHandler(PreparationService service)
    : ICommandHandler<PreparationMediaUploadCommand, AppResult<MediaAsset>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<MediaAsset>> HandleAsync(PreparationMediaUploadCommand request, CancellationToken cancellationToken)
    {
        var asset = await service.CreateMediaAsync(request.Access, request.FileName, request.Provenance, request.Bytes, cancellationToken);
        return AppResult<MediaAsset>.Ok(asset);
    }
}
