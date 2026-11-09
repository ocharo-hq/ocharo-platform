using BrandWorkspace.Application.Access.Models;
using BrandWorkspace.Application.Access.Queries;
using BrandWorkspace.Application.Access.Repositories;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Infrastructure.Access.QueryHandlers;

/// <summary>Handles <see cref="OrganizationGetAllQuery"/>.</summary>
public sealed class OrganizationGetAllQueryHandler(IAccessRepository repository)
    : IQueryHandler<OrganizationGetAllQuery, AppResult<IReadOnlyList<Organization>>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<IReadOnlyList<Organization>>> HandleAsync(OrganizationGetAllQuery request, CancellationToken cancellationToken)
    {
        var organizations = new List<Organization>();
        if (request.OrganizationId is { } id && request.Role is { } role)
        {
            await repository.ProjectOrganizationAsync(id, request.Name, request.Slug, cancellationToken);
            if (await repository.FindOrganizationAsync(id, role, cancellationToken) is { } organization)
                organizations.Add(organization);
        }
        return AppResult<IReadOnlyList<Organization>>.Ok(organizations);
    }
}
