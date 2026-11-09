using BrandWorkspace.Application.Commerce.Models;
using BrandWorkspace.Application.Commerce.Queries;
using BrandWorkspace.Application.Commerce.Repositories;
using BrandWorkspace.Application.Foundation.Errors;
using BrandWorkspace.Infrastructure.Commerce.Services;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Infrastructure.Commerce.QueryHandlers;

/// <summary>Handles <see cref="CommerceGetQuery"/>.</summary>
public sealed class CommerceGetQueryHandler(ICommerceRepository repository)
    : IQueryHandler<CommerceGetQuery, AppResult<CommerceDraftLookup>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<CommerceDraftLookup>> HandleAsync(CommerceGetQuery request, CancellationToken cancellationToken)
    {
        var draft = await repository.GetAsync(request.Access.OrganizationId, request.BrandId, cancellationToken);
        return AppResult<CommerceDraftLookup>.Ok(new CommerceDraftLookup(draft));
    }
}
