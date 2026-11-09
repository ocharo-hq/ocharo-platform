using BrandWorkspace.Application.Preparation.Commands;
using BrandWorkspace.Domain.Preparation.Models;
using BrandWorkspace.Infrastructure.Foundation.Extensions;
using BrandWorkspace.Infrastructure.Preparation.Services;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Infrastructure.Preparation.CommandHandlers;

/// <summary>Handles <see cref="DraftReviewCommand"/>.</summary>
public sealed class DraftReviewCommandHandler(PreparationService service)
    : ICommandHandler<DraftReviewCommand, AppResult<Draft>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<Draft>> HandleAsync(
        DraftReviewCommand request,
        CancellationToken cancellationToken)
    {
        Result<Draft> result = await service.ReviewAsync(
            request.Access,
            request.DraftId,
            request.ExpectedRevision,
            request.Action,
            request.Reason,
            cancellationToken);
        return result.ToAppResult();
    }
}
