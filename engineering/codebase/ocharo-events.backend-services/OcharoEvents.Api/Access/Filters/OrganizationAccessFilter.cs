using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using OcharoEvents.Application.Access.Models;
using OcharoEvents.Application.Events.Errors;
using WoW.Two.Sdk.Beta.Be.Identity.Core.Organizations;
using WoW.Two.Sdk.Beta.Be.Web.ExceptionHandling.Extensions;

namespace OcharoEvents.Api.Access.Filters;

/// <summary>Resolves the signed-in person's organization access from the Identity claims before an organization-scoped
/// action runs (events-app.md #4): no session answers 401; a route organization other than the session's active one, or
/// one where the person lacks <see cref="EventsPermissions.Read"/>, answers 404, as Desk's filter did, so another
/// organization's events are never disclosed.</summary>
public sealed class OrganizationAccessFilter : IAsyncActionFilter
{
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
            context.Result = controller.ToProblem(OcharoEventsErrors.Unauthorized("Authentication required"));
            return;
        }
        if (!Guid.TryParse(http.GetRouteValue(OrganizationRouteKey)?.ToString(), out var organizationId)
            || http.User.FindFirstValue(OrganizationClaimTypeConstants.Id)
                != organizationId.ToString("D", CultureInfo.InvariantCulture)
            || !http.User.HasOrganizationPermission(organizationId, EventsPermissions.Read))
        {
            context.Result = controller.ToProblem(OcharoEventsErrors.NotFound("Not found"));
            return;
        }
        http.Items[AccessKey] = new OrganizationAccess(organizationId, AccessClaims.UserId(http.User),
            http.User.HasOrganizationPermission(organizationId, EventsPermissions.Track));
        await next();
    }
}
