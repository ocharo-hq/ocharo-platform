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
}
