using BrandWorkspace.Application.Sourcing.Commands;
using BrandWorkspace.Domain.Sourcing.Models;
using BrandWorkspace.Infrastructure.Foundation.Extensions;
using BrandWorkspace.Infrastructure.Sourcing.Services;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Infrastructure.Sourcing.CommandHandlers;

/// <summary>Handles <see cref="SourcingMediaUpdateCommand"/>.</summary>
public sealed class SourcingMediaUpdateCommandHandler(SourcingService service, SourcingValidationService validation)
    : ICommandHandler<SourcingMediaUpdateCommand, AppResult<SourcingMedia>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<SourcingMedia>> HandleAsync(
        SourcingMediaUpdateCommand request,
        CancellationToken cancellationToken)
    {
        var input = validation.Media(request.SupplierId, request.Capture, request.Caption);
        Result<SourcingMedia> updated = await service.UpdateMediaAsync(
            request.Access,
            request.MediaId,
            request.ExpectedRevision,
            input,
            cancellationToken);
        return updated.ToAppResult();
    }
}
