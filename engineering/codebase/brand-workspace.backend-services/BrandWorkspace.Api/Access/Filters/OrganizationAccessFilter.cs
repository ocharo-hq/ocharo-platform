using System.Globalization;
using System.Security.Claims;
using BrandWorkspace.Application.Access.Commands;
using BrandWorkspace.Application.Access.Models;
using BrandWorkspace.Application.Foundation.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using WoW.Two.Sdk.Beta.Be.Identity.Core.Organizations;
using WoW.Two.Sdk.Beta.Be.Mediator;
using WoW.Two.Sdk.Beta.Be.Web.ExceptionHandling.Extensions;

namespace BrandWorkspace.Api.Access.Filters;

/// <summary>Resolves the signed-in person's organization access from the Identity claims before an organization-scoped
/// action runs, and projects that person into Desk so authored records keep their foreign keys.</summary>
/// <param name="sender">The mediator that projects the person and the organization.</param>
public sealed class OrganizationAccessFilter(ISender sender) : IAsyncActionFilter
{
    /// <summary>Holds the organization permission that lets a member edit.</summary>
    public const string EditPermission = "desk:edit";

    /// <summary>Holds the organization permission that lets a member approve and publish.</summary>
    public const string ApprovePermission = "desk:approve";

    /// <summary>Holds the route value naming the organization.</summary>
    private const string OrganizationRouteKey = "organizationId";

    /// <summary>Holds the request item the resolved access is kept under.</summary>
    private static readonly object AccessKey = new();

    /// <summary>Gets the access this filter resolved for the request.</summary>
    /// <param name="context">The request's HTTP context.</param>
    public static OrganizationAccess GetAccess(HttpContext context)
    {
        return context.Items.TryGetValue(AccessKey, out var value) && value is OrganizationAccess access
            ? access
            : throw new InvalidOperationException("Organization access filter must execute first.");
    }

    /// <inheritdoc />
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);
        var http = context.HttpContext;
        var controller = (ControllerBase)context.Controller;
        if (http.User.Identity?.IsAuthenticated != true)
        {
            context.Result = controller.ToProblem(DeskErrors.Unauthorized("Authentication required"));
            return;
        }
        if (!Guid.TryParse(http.GetRouteValue(OrganizationRouteKey)?.ToString(), out var organizationId)
            || http.User.FindFirstValue(OrganizationClaimTypeConstants.Id)
                != organizationId.ToString("D", CultureInfo.InvariantCulture)
            || http.User.GetOrganizationRole(organizationId) is not { } role)
        {
            context.Result = controller.ToProblem(DeskErrors.NotFound("Not found"));
            return;
        }
        var access = new OrganizationAccess(organizationId, AccessClaims.UserId(http.User), role,
            http.User.HasOrganizationPermission(organizationId, EditPermission),
            http.User.HasOrganizationPermission(organizationId, ApprovePermission));
        var projected = await sender.SendAsync(new MemberProjectCommand
        {
            Access = access,
            OrganizationName = AccessClaims.OrganizationName(http.User),
            OrganizationSlug = AccessClaims.OrganizationSlug(http.User),
            DisplayName = http.User.FindFirstValue("name"),
            Email = http.User.FindFirstValue("email"),
        }, http.RequestAborted);
        var refusal = projected.Match<IActionResult?>(_ => null, fail => controller.ToProblem(fail.Error));
        if (refusal is not null)
        {
            context.Result = refusal;
            return;
        }
        http.Items[AccessKey] = access;
        await next();
    }
}
