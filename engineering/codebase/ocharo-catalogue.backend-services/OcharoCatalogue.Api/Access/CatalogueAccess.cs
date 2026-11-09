using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Beta.Be.Identity.Core.Organizations;

namespace OcharoCatalogue.Api.Access;

/// <summary>
/// Who may use the catalogue: a person signed in through Ocharo's Identity whose active organization (<c>org_id</c>) is
/// the one this catalogue belongs to (<see cref="CatalogueAccessOptions.OrganizationId"/>) and who holds a role there.
/// Reads need that membership, unsafe methods also need <see cref="EditPermission"/>, and publishing or withdrawing a
/// release also needs <see cref="ApprovePermission"/>.
/// </summary>
public static class CatalogueAccess
{
    /// <summary>Holds the permission every unsafe catalogue request needs.</summary>
    public const string EditPermission = "catalogue:edit";

    /// <summary>Holds the permission publishing and withdrawing a release need.</summary>
    public const string ApprovePermission = "catalogue:approve";

    /// <summary>Holds the policy every endpoint without its own falls back to: membership, and edit for unsafe methods.</summary>
    public const string MemberPolicy = "CatalogueMember";

    /// <summary>Holds the policy of the publication actions: <see cref="MemberPolicy"/> plus <see cref="ApprovePermission"/>.</summary>
    public const string ApprovePolicy = "CatalogueApprover";

    /// <summary>Whether <paramref name="principal"/> is signed in with <paramref name="organizationId"/> as its active
    /// organization and holds a role there.</summary>
    public static bool IsMember(ClaimsPrincipal principal, Guid organizationId) =>
        principal.Identity?.IsAuthenticated == true && organizationId != Guid.Empty
        && principal.FindFirstValue(OrganizationClaimTypeConstants.Id) == organizationId.ToString("D", CultureInfo.InvariantCulture)
        && principal.GetOrganizationRole(organizationId) is not null;

    /// <summary>Whether <paramref name="principal"/> may send <paramref name="method"/>, with <paramref name="approval"/>
    /// for a publication action.</summary>
    public static bool IsAllowed(ClaimsPrincipal principal, Guid organizationId, string method, bool approval) =>
        IsMember(principal, organizationId)
        && (IsSafe(method) || principal.HasOrganizationPermission(organizationId, EditPermission))
        && (!approval || principal.HasOrganizationPermission(organizationId, ApprovePermission));

    private static bool IsSafe(string method) =>
        HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method);
}
