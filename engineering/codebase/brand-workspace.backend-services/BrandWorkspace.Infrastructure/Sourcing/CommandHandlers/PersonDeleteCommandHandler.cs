using BrandWorkspace.Application.Sourcing.Commands;
using BrandWorkspace.Infrastructure.Foundation.Extensions;
using BrandWorkspace.Infrastructure.Sourcing.Services;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Infrastructure.Sourcing.CommandHandlers;

/// <summary>Handles <see cref="PersonDeleteCommand"/>.</summary>
public sealed class PersonDeleteCommandHandler(SourcingService service)
    : ICommandHandler<PersonDeleteCommand, AppResult<Guid>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<Guid>> HandleAsync(
        PersonDeleteCommand request,
        CancellationToken cancellationToken)
    {
        Result deleted = await service.DeletePersonAsync(
            request.Access,
            request.PersonId,
            request.ExpectedRevision,
            cancellationToken);
        return deleted.ToAppResult(request.PersonId);
    }
}
