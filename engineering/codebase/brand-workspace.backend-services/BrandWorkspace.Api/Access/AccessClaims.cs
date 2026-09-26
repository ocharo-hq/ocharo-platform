using System.Security.Claims;

namespace BrandWorkspace.Api.Access;

/// <summary>Names and reads the authenticated operator/session claims.</summary>
public static class AccessClaims
{
    public const string SessionId = "brand_workspace_session";
    public static string UserId(ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("Authenticated user id is missing.");
    public static Guid? Session(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(SessionId), out var id) ? id : null;
}
