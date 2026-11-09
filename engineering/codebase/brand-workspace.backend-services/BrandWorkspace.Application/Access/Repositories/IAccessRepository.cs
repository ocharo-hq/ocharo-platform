using BrandWorkspace.Application.Access.Models;

namespace BrandWorkspace.Application.Access.Repositories;

/// <summary>Reads Desk's organizations and projects signed-in people; the membership read serves the import commands
/// until migration 011.</summary>
public interface IAccessRepository
{
    Task<Organization?> FindOrganizationAsync(Guid organizationId, string role, CancellationToken token);
    Task<OrganizationAccess?> FindMembershipAsync(Guid organizationId, string userId, CancellationToken token);
    Task ProjectUserAsync(string userId, string? displayName, string? email, CancellationToken token);
    Task ProjectOrganizationAsync(Guid organizationId, string? name, string? slug, CancellationToken token);
}
