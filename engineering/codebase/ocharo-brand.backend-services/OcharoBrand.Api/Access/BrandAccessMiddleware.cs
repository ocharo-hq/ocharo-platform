using System.Security.Claims;
using Microsoft.Extensions.Options;
using OcharoBrand.Api.Access.Extensions;
using OcharoBrand.Application.Brand.Errors;
using WoW.Two.Sdk.Beta.Be.Identity.Core.Organizations;

namespace OcharoBrand.Api.Access;

/// <summary>
/// Admits a session to the private API only as a member of the bound organization (<c>Brand:OrganizationId</c>): the
/// session's active organization must be it and carry a role there. Reads need the membership, unsafe methods
/// <see cref="BrandAccess.EditPermission"/>, publication <see cref="BrandAccess.ApprovePermission"/> too. Runs after
/// <c>UseIdentityBff</c>, which answers 401 without a session and checks the session's <c>X-Csrf-Token</c>; public
/// requests and the session routes pass untouched. Another organization's session answers 403, as does a missing
/// permission; each refusal names itself in the problem's <c>brandCode</c>.
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
            return http.WriteProblemAsync(BrandErrors.SignInRequired());
        var organization = options.Value.OrganizationId;
        if (!Guid.TryParse(user.FindFirstValue(OrganizationClaimTypeConstants.Id), out var active) || active != organization
            || user.GetOrganizationRole(organization) is null)
            return http.WriteProblemAsync(BrandErrors.OrganizationAccessRequired());
        if (BrandAccess.IsUnsafe(http.Request.Method) && !user.HasOrganizationPermission(organization, BrandAccess.EditPermission))
            return http.WriteProblemAsync(BrandErrors.EditPermissionRequired());
        if (BrandAccess.IsPublication(http.Request) && !user.HasOrganizationPermission(organization, BrandAccess.ApprovePermission))
            return http.WriteProblemAsync(BrandErrors.ApprovalPermissionRequired());
        return next(http);
    }
}
