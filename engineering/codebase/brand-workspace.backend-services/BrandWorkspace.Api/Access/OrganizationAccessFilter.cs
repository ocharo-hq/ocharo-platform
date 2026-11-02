using System.Globalization;
using System.Security.Claims;
using BrandWorkspace.Application.Access;
using WoW.Two.Sdk.Beta.Be.Identity.Core.Organizations;

namespace BrandWorkspace.Api.Access;

/// <summary>Resolves the signed-in person's organization access from the Identity claims before an organization-scoped
/// endpoint executes, and projects that person into Desk so authored records keep their foreign keys.</summary>
public sealed class OrganizationAccessFilter(IAccessRepository repository) : IEndpointFilter
{
    public const string EditPermission = "desk:edit";
    public const string ApprovePermission = "desk:approve";
    private static readonly object AccessKey = new();

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        if (http.User.Identity?.IsAuthenticated != true)
            return AccessProblems.Create(401, "Authentication required", "authentication_required");
        if (!Guid.TryParse(http.Request.RouteValues["organizationId"]?.ToString(), out var organizationId)
            || http.User.FindFirstValue(OrganizationClaimTypeConstants.Id) != organizationId.ToString("D", CultureInfo.InvariantCulture)
            || http.User.GetOrganizationRole(organizationId) is not { } role)
            return AccessProblems.Create(404, "Not found", "not_found");
        var userId = AccessClaims.UserId(http.User);
        await repository.ProjectOrganizationAsync(organizationId, AccessClaims.OrganizationName(http.User), AccessClaims.OrganizationSlug(http.User), http.RequestAborted);
        await repository.ProjectUserAsync(userId, http.User.FindFirstValue("name"), http.User.FindFirstValue("email"), http.RequestAborted);
        http.Items[AccessKey] = new OrganizationAccess(organizationId, userId, role,
            http.User.HasOrganizationPermission(organizationId, EditPermission),
            http.User.HasOrganizationPermission(organizationId, ApprovePermission));
        return await next(context);
    }

    public static OrganizationAccess GetAccess(HttpContext context) => context.Items.TryGetValue(AccessKey, out var value)
        && value is OrganizationAccess access ? access : throw new InvalidOperationException("Organization access filter must execute first.");
}
