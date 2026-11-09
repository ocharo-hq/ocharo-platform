using BrandWorkspace.Application.Commerce.Queries;
using BrandWorkspace.Application.Commerce.Repositories;
using BrandWorkspace.Application.Foundation.Errors;
using BrandWorkspace.Domain.Commerce;
using BrandWorkspace.Infrastructure.Commerce.Services;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Infrastructure.Commerce.QueryHandlers;

/// <summary>Handles <see cref="CreativeImportGetAllQuery"/>.</summary>
public sealed class CreativeImportGetAllQueryHandler(ICommerceRepository repository)
    : IQueryHandler<CreativeImportGetAllQuery, AppResult<IReadOnlyList<CreativeImport>>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<IReadOnlyList<CreativeImport>>> HandleAsync(CreativeImportGetAllQuery request, CancellationToken cancellationToken)
    {
        var imports = await repository.CreativeImportsAsync(request.Access.OrganizationId, request.BrandId, cancellationToken);
        return AppResult<IReadOnlyList<CreativeImport>>.Ok(imports);
    }
}
