using System.Security.Claims;
using BrandWorkspace.Application.Access.Models;
using BrandWorkspace.Application.Access.Queries;
using Microsoft.AspNetCore.Mvc;
using WoW.Two.Sdk.Beta.Be.Identity.Core.Organizations;
using WoW.Two.Sdk.Beta.Be.Mediator;
using WoW.Two.Sdk.Beta.Be.Web.Contracts;
using WoW.Two.Sdk.Beta.Be.Web.ExceptionHandling.Extensions;

namespace BrandWorkspace.Api.Access.Controllers;

/// <summary>Exposes the signed-in person's organizations over HTTP.</summary>
[ApiController]
[Route("api/organizations")]
public sealed class OrganizationsController(ISender sender) : ControllerBase
{
    /// <summary>Gets the session's active organization, when its claims carry one with a role.</summary>
    /// <param name="cancellationToken">The token that cancels the request.</param>
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        Guid? id = Guid.TryParse(User.FindFirstValue(OrganizationClaimTypeConstants.Id), out var parsed)
            ? parsed
            : null;
        var result = await sender.SendAsync(new OrganizationGetAllQuery
        {
            OrganizationId = id,
            Role = id is { } organizationId ? User.GetOrganizationRole(organizationId) : null,
            Name = AccessClaims.OrganizationName(User),
            Slug = AccessClaims.OrganizationSlug(User),
        }, cancellationToken);
        return result.Match<IActionResult>(
            ok => Ok(ApiResponse<IReadOnlyList<Organization>>.Ok(ok.Data)),
            fail => this.ToProblem(fail.Error));
    }
}
