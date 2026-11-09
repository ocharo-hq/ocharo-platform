using BrandWorkspace.Application.Sourcing.Queries;
using BrandWorkspace.Domain.Sourcing.Models;
using BrandWorkspace.Infrastructure.Sourcing.Services;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Infrastructure.Sourcing.QueryHandlers;

/// <summary>Handles <see cref="SupplierGetAllQuery"/>.</summary>
public sealed class SupplierGetAllQueryHandler(SourcingService service)
    : IQueryHandler<SupplierGetAllQuery, AppResult<IReadOnlyList<SupplierSummary>>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<IReadOnlyList<SupplierSummary>>> HandleAsync(SupplierGetAllQuery request, CancellationToken cancellationToken)
    {
        var suppliers = await service.GetSuppliersAsync(request.Access, request.Query, request.Stage, request.Limit, request.Offset,
            cancellationToken);
        return AppResult<IReadOnlyList<SupplierSummary>>.Ok(suppliers);
    }
}
