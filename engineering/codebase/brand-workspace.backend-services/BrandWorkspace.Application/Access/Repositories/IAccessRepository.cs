using BrandWorkspace.Application.Access.Models;

namespace BrandWorkspace.Application.Access.Repositories;

/// <summary>Reads and projects Desk's organizations and the people Identity signed in.</summary>
public interface IAccessRepository
{
    Task<Organization?> FindOrganizationAsync(Guid organizationId, string role, CancellationToken token);

    /// <summary>
    /// Finds a person Desk has projected from Identity by their Identity user id (<c>sub</c>), else by the email their
    /// sign-in carried when exactly one projected person holds it; null for anyone who never signed in to Desk.
    /// </summary>
    Task<string?> FindPersonAsync(string idOrEmail, CancellationToken token);

    Task ProjectUserAsync(string userId, string? displayName, string? email, CancellationToken token);
    Task ProjectOrganizationAsync(Guid organizationId, string? name, string? slug, CancellationToken token);
}
