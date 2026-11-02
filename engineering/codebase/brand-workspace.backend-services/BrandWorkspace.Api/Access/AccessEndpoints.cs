using System.Security.Claims;
using BrandWorkspace.Application.Access;
using WoW.Two.Sdk.Beta.Be.Identity.Bff;
using WoW.Two.Sdk.Beta.Be.Identity.Core.Organizations;

namespace BrandWorkspace.Api.Access;

/// <summary>Maps the Identity session routes and the signed-in person's organization.</summary>
public static class AccessEndpoints
{
    public static WebApplication MapAccessEndpoints(this WebApplication app)
    {
        app.MapIdentityBffEndpoints();
        app.MapGet("/api/organizations", async (HttpContext http, IAccessRepository repository) =>
        {
            var organizations = new List<Organization>();
            if (Guid.TryParse(http.User.FindFirstValue(OrganizationClaimTypeConstants.Id), out var id)
                && http.User.GetOrganizationRole(id) is { } role)
            {
                await repository.ProjectOrganizationAsync(id, AccessClaims.OrganizationName(http.User), AccessClaims.OrganizationSlug(http.User), http.RequestAborted);
                if (await repository.FindOrganizationAsync(id, role, http.RequestAborted) is { } organization)
                    organizations.Add(organization);
            }
            return Results.Ok(new { data = organizations });
        });
        return app;
    }
}
