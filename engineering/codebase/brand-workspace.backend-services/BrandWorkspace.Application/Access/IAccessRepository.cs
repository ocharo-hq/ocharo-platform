namespace BrandWorkspace.Application.Access;

/// <summary>Reads Desk's organizations and projects signed-in people; the legacy session and membership reads serve
/// integration tickets and the import commands until migration 011.</summary>
public interface IAccessRepository
{
    Task<bool> IsSessionActiveAsync(Guid sessionId, string userId, DateTimeOffset now, CancellationToken token);
    Task<Organization?> FindOrganizationAsync(Guid organizationId, string role, CancellationToken token);
    Task<OrganizationAccess?> FindMembershipAsync(Guid organizationId, string userId, CancellationToken token);
    Task<OperatorProfile?> FindProfileAsync(string userId, CancellationToken token);
    Task ProjectUserAsync(string userId, string? displayName, string? email, CancellationToken token);
    Task ProjectOrganizationAsync(Guid organizationId, string? name, string? slug, CancellationToken token);
}
