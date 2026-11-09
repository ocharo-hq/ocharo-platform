using BrandWorkspace.Application.Preparation.Commands;
using BrandWorkspace.Domain.Preparation.Models;
using BrandWorkspace.Infrastructure.Foundation.Extensions;
using BrandWorkspace.Infrastructure.Preparation.Services;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Infrastructure.Preparation.CommandHandlers;

/// <summary>Handles <see cref="DraftSaveCommand"/>.</summary>
public sealed class DraftSaveCommandHandler(PreparationService service)
    : ICommandHandler<DraftSaveCommand, AppResult<Draft>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<Draft>> HandleAsync(
        DraftSaveCommand request,
        CancellationToken cancellationToken)
    {
        Result<Draft> result = await service.SaveDraftAsync(
            request.Access,
            request.DraftId,
            request.ExpectedRevision,
            request.Input,
            cancellationToken);
        return result.ToAppResult();
    }
}
