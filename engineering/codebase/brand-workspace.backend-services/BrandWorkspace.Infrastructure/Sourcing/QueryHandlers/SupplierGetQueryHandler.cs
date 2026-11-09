using BrandWorkspace.Application.Sourcing.Queries;
using BrandWorkspace.Domain.Sourcing.Models;
using BrandWorkspace.Infrastructure.Sourcing.Services;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Infrastructure.Sourcing.QueryHandlers;

/// <summary>Handles <see cref="SupplierGetQuery"/>.</summary>
public sealed class SupplierGetQueryHandler(SourcingService service)
    : IQueryHandler<SupplierGetQuery, AppResult<Supplier>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<Supplier>> HandleAsync(SupplierGetQuery request, CancellationToken cancellationToken)
    {
        return AppResult<Supplier>.Ok(await service.GetSupplierAsync(request.Access, request.SupplierId, cancellationToken));
    }
}
