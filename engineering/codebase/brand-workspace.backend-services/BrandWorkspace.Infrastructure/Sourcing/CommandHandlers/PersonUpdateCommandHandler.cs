using BrandWorkspace.Application.Sourcing.Commands;
using BrandWorkspace.Domain.Sourcing.Models;
using BrandWorkspace.Infrastructure.Foundation.Extensions;
using BrandWorkspace.Infrastructure.Sourcing.Services;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Infrastructure.Sourcing.CommandHandlers;

/// <summary>Handles <see cref="PersonUpdateCommand"/>.</summary>
public sealed class PersonUpdateCommandHandler(SourcingService service, SourcingValidationService validation)
    : ICommandHandler<PersonUpdateCommand, AppResult<SupplierPerson>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<SupplierPerson>> HandleAsync(
        PersonUpdateCommand request,
        CancellationToken cancellationToken)
    {
        var input = validation.Person(request.Name, request.Role, request.Notes);
        Result<SupplierPerson> updated = await service.UpdatePersonAsync(
            request.Access,
            request.PersonId,
            request.ExpectedRevision,
            input,
            cancellationToken);
        return updated.ToAppResult();
    }
}
