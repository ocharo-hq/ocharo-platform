using BrandWorkspace.Application.Sourcing.Commands;
using BrandWorkspace.Domain.Sourcing.Models;
using BrandWorkspace.Infrastructure.Foundation.Extensions;
using BrandWorkspace.Infrastructure.Sourcing.Services;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Infrastructure.Sourcing.CommandHandlers;

/// <summary>Handles <see cref="PersonCreateCommand"/>.</summary>
public sealed class PersonCreateCommandHandler(SourcingService service, SourcingValidationService validation)
    : ICommandHandler<PersonCreateCommand, AppResult<SupplierPerson>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<SupplierPerson>> HandleAsync(
        PersonCreateCommand request,
        CancellationToken cancellationToken)
    {
        var input = validation.Person(request.Name, request.Role, request.Notes);
        Result<SupplierPerson> created = await service.CreatePersonAsync(
            request.Access,
            request.SupplierId,
            input,
            cancellationToken);
        return created.ToAppResult();
    }
}
