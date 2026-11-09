using BrandWorkspace.Application.Sourcing.Commands;
using BrandWorkspace.Infrastructure.Sourcing.Services;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Infrastructure.Sourcing.CommandHandlers;

/// <summary>Handles <see cref="PersonDeleteCommand"/>.</summary>
public sealed class PersonDeleteCommandHandler(SourcingService service)
    : ICommandHandler<PersonDeleteCommand, AppResult<Guid>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<Guid>> HandleAsync(PersonDeleteCommand request, CancellationToken cancellationToken)
    {
        await service.DeletePersonAsync(request.Access, request.PersonId, request.ExpectedRevision, cancellationToken);
        return AppResult<Guid>.Ok(request.PersonId);
    }
}
