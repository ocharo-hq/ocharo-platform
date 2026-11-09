using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Beta.Be.Identity.Core.Organizations;

namespace OcharoCatalogue.Api.Access;

/// <summary>Validates <see cref="CatalogueAccessRequirement"/> against the request's method and the bound organization.
/// A failure of a signed-in person answers the BFF's 403 problem, as a missing permission does.</summary>
public sealed class CatalogueAccessHandler(IOptions<CatalogueAccessOptions> options, IHttpContextAccessor accessor)
    : AuthorizationHandler<CatalogueAccessRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, CatalogueAccessRequirement requirement)
    {
        var http = context.Resource as HttpContext ?? accessor.HttpContext;
        if (http is not null
            && CatalogueAccess.IsAllowed(context.User, options.Value.OrganizationId, http.Request.Method, requirement.RequiresApproval))
            context.Succeed(requirement);
        return Task.CompletedTask;
    }
}
