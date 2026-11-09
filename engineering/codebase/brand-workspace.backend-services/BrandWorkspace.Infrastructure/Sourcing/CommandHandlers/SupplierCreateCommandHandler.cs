using BrandWorkspace.Application.Sourcing.Commands;
using BrandWorkspace.Domain.Sourcing.Models;
using BrandWorkspace.Infrastructure.Foundation.Extensions;
using BrandWorkspace.Infrastructure.Sourcing.Services;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Infrastructure.Sourcing.CommandHandlers;

/// <summary>Handles <see cref="SupplierCreateCommand"/>.</summary>
public sealed class SupplierCreateCommandHandler(SourcingService service, SourcingValidationService validation)
    : ICommandHandler<SupplierCreateCommand, AppResult<Supplier>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<Supplier>> HandleAsync(
        SupplierCreateCommand request,
        CancellationToken cancellationToken)
    {
        var input = validation.Supplier(
            request.Name,
            request.Stage,
            request.Country,
            request.City,
            request.Website,
            request.Notes);
        Result<Supplier> created = await service.CreateSupplierAsync(request.Access, input, cancellationToken);
        return created.ToAppResult();
    }
}
