using BrandWorkspace.Application.Access;

namespace BrandWorkspace.Api.Access;

/// <summary>Resolves live membership before an organization-scoped endpoint executes.</summary>
public sealed class OrganizationAccessFilter(IAccessRepository repository) : IEndpointFilter
{
    private static readonly object AccessKey = new();

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        if (http.User.Identity?.IsAuthenticated != true)
            return AccessProblems.Create(401, "Authentication required", "authentication_required");
        if (!Guid.TryParse(http.Request.RouteValues["organizationId"]?.ToString(), out var organizationId))
            return AccessProblems.Create(404, "Not found", "not_found");
        var access = await repository.FindMembershipAsync(organizationId, AccessClaims.UserId(http.User), http.RequestAborted);
        if (access is null) return AccessProblems.Create(404, "Not found", "not_found");
        http.Items[AccessKey] = access;
        return await next(context);
    }

    public static OrganizationAccess GetAccess(HttpContext context) => context.Items.TryGetValue(AccessKey, out var value)
        && value is OrganizationAccess access ? access : throw new InvalidOperationException("Organization access filter must execute first.");
}
