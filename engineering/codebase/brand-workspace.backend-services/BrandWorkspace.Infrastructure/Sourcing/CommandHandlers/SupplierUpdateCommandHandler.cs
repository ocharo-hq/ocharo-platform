using BrandWorkspace.Application.Sourcing.Commands;
using BrandWorkspace.Domain.Sourcing.Models;
using BrandWorkspace.Infrastructure.Sourcing.Services;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Infrastructure.Sourcing.CommandHandlers;

/// <summary>Handles <see cref="SupplierUpdateCommand"/>.</summary>
public sealed class SupplierUpdateCommandHandler(SourcingService service, SourcingValidationService validation)
    : ICommandHandler<SupplierUpdateCommand, AppResult<Supplier>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<Supplier>> HandleAsync(SupplierUpdateCommand request, CancellationToken cancellationToken)
    {
        var input = validation.Supplier(request.Name, request.Stage, request.Country, request.City, request.Website, request.Notes);
        var updated = await service.UpdateSupplierAsync(request.Access, request.SupplierId, request.ExpectedRevision, input,
            cancellationToken);
        return AppResult<Supplier>.Ok(updated);
    }
}
