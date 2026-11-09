using OcharoCatalogue.Application.Catalogue.Core.Repositories;
using OcharoCatalogue.Application.Catalogue.Designs.Commands;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace OcharoCatalogue.Infrastructure.Catalogue.Designs.CommandHandlers;

/// <summary>Handles <see cref="DesignArchiveCommand"/>.</summary>
public sealed class DesignArchiveCommandHandler(IDesignRepository designs, TimeProvider timeProvider)
    : ICommandHandler<DesignArchiveCommand, AppResult<Guid>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<Guid>> HandleAsync(DesignArchiveCommand request, CancellationToken ct)
    {
        await designs.ArchiveAsync(
            request.Id,
            request.ExpectedRevision,
            timeProvider.GetUtcNow(),
            ct);
        return AppResult<Guid>.Ok(request.Id);
    }
}
