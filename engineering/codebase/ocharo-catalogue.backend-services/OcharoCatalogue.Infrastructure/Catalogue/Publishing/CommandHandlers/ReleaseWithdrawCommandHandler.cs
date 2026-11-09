using OcharoCatalogue.Application.Catalogue.Core.Repositories;
using OcharoCatalogue.Application.Catalogue.Publishing.Commands;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Infrastructure.Catalogue.Publishing.CommandHandlers;

/// <summary>Handles <see cref="ReleaseWithdrawCommand"/>.</summary>
public sealed class ReleaseWithdrawCommandHandler(IDesignRepository designs, TimeProvider timeProvider)
    : ICommandHandler<ReleaseWithdrawCommand, AppResult<Guid>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<Guid>> HandleAsync(ReleaseWithdrawCommand request, CancellationToken ct)
    {
        await designs.WithdrawAsync(
            request.DesignId,
            request.ExpectedRevision,
            timeProvider.GetUtcNow(),
            ct);
        return AppResult<Guid>.Ok(request.DesignId);
    }
}
