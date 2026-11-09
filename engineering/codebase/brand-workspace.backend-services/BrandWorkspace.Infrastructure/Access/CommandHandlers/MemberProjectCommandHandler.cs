using BrandWorkspace.Application.Access.Commands;
using BrandWorkspace.Application.Access.Models;
using BrandWorkspace.Application.Access.Repositories;
using WoW.Two.Sdk.Beta.Be.Mediator.Cqrs;
using WoW.Two.Sdk.Beta.Be.Mediator.Result;

namespace BrandWorkspace.Infrastructure.Access.CommandHandlers;

/// <summary>Handles <see cref="MemberProjectCommand"/>.</summary>
public sealed class MemberProjectCommandHandler(IAccessRepository repository)
    : ICommandHandler<MemberProjectCommand, AppResult<OrganizationAccess>>
{
    /// <inheritdoc />
    public async ValueTask<AppResult<OrganizationAccess>> HandleAsync(MemberProjectCommand request, CancellationToken cancellationToken)
    {
        var access = request.Access;
        await repository.ProjectOrganizationAsync(access.OrganizationId, request.OrganizationName, request.OrganizationSlug, cancellationToken);
        await repository.ProjectUserAsync(access.UserId, request.DisplayName, request.Email, cancellationToken);
        return AppResult<OrganizationAccess>.Ok(access);
    }
}
