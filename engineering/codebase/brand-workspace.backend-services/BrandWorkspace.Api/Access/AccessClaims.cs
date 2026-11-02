using System.Security.Claims;

namespace BrandWorkspace.Api.Access;

/// <summary>Reads the person and sign-in session the Identity instance names (<c>sub</c>, <c>sid</c>).</summary>
public static class AccessClaims
{
    public const string Subject = "sub";
    public const string SessionId = "sid";
    public static string UserId(ClaimsPrincipal principal) =>
        principal.FindFirstValue(Subject) ?? throw new InvalidOperationException("Authenticated user id is missing.");
    public static string? Session(ClaimsPrincipal principal) => principal.FindFirstValue(SessionId);
    /// <summary>The active organization's name and slug, once the Identity instance sends them; Desk names its projection from them.</summary>
    public const string OrganizationNameClaim = "org_name";
    public const string OrganizationSlugClaim = "org_slug";
    public static string? OrganizationName(ClaimsPrincipal principal) => principal.FindFirstValue(OrganizationNameClaim);
    public static string? OrganizationSlug(ClaimsPrincipal principal) => principal.FindFirstValue(OrganizationSlugClaim);
}
