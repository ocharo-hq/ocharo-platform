namespace BrandWorkspace.Application.Access;

/// <summary>Persists revocable sessions and organization membership authority.</summary>
public interface IAccessRepository
{
    Task CreateSessionAsync(Guid sessionId, string userId, string securityStamp, DateTimeOffset expiresAt,
        Guid? replacedSessionId, CancellationToken token);
    Task<bool> IsSessionActiveAsync(Guid sessionId, string userId, DateTimeOffset now, CancellationToken token);
    Task RevokeSessionAsync(Guid sessionId, DateTimeOffset now, CancellationToken token);
    Task<IReadOnlyList<Organization>> ListOrganizationsAsync(string userId, CancellationToken token);
    Task<OrganizationAccess?> FindMembershipAsync(Guid organizationId, string userId, CancellationToken token);
    Task<Organization?> CreateOrganizationAsync(string name, string slug, string userId, CancellationToken token);
}
