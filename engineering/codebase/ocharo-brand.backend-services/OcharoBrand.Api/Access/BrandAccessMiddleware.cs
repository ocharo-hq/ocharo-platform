using System.Security.Claims;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Beta.Be.Identity.Core.Organizations;

namespace OcharoBrand.Api.Access;

/// <summary>
/// Admits a session to the private API only as a member of the bound organization (<c>Brand:OrganizationId</c>): the
/// session's active organization must be it and carry a role there. Reads need the membership, unsafe methods
/// <see cref="BrandAccess.EditPermission"/>, publication <see cref="BrandAccess.ApprovePermission"/> too. Runs after
/// <c>UseIdentityBff</c>, which answers 401 without a session and checks the session's <c>X-Csrf-Token</c>; public
/// requests and the session routes pass untouched. Another organization's session answers 403, as does a missing permission.
/// </summary>
public sealed class BrandAccessMiddleware(RequestDelegate next, IOptions<BrandAccessOptions> options)
{
    public Task InvokeAsync(HttpContext http)
    {
        var path = http.Request.Path;
        if (!path.StartsWithSegments("/api") || path.StartsWithSegments("/api/identity") || BrandAccess.IsPublic(http))
            return next(http);
        var user = http.User;
        if (user.Identity?.IsAuthenticated != true)
            return ProblemAsync(http, StatusCodes.Status401Unauthorized, "Sign in to continue", "sign_in_required");
        var organization = options.Value.OrganizationId;
        if (!Guid.TryParse(user.FindFirstValue(OrganizationClaimTypeConstants.Id), out var active) || active != organization
            || user.GetOrganizationRole(organization) is null)
            return ProblemAsync(http, StatusCodes.Status403Forbidden, "This account is not a member of the Ocharo organization", "organization_access_required");
        if (BrandAccess.IsUnsafe(http.Request.Method) && !user.HasOrganizationPermission(organization, BrandAccess.EditPermission))
            return ProblemAsync(http, StatusCodes.Status403Forbidden, "Editing access is required", "edit_permission_required");
        if (BrandAccess.IsPublication(http.Request) && !user.HasOrganizationPermission(organization, BrandAccess.ApprovePermission))
            return ProblemAsync(http, StatusCodes.Status403Forbidden, "Publication approval access is required", "approval_permission_required");
        return next(http);
    }

    private static Task ProblemAsync(HttpContext http, int status, string title, string code) =>
        Results.Problem(statusCode: status, title: title, extensions: new Dictionary<string, object?> { ["code"] = code }).ExecuteAsync(http);
}
