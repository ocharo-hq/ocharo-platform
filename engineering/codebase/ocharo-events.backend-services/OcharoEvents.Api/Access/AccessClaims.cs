using System.Security.Claims;

namespace OcharoEvents.Api.Access;

/// <summary>Reads the person the Identity instance names (<c>sub</c>).</summary>
public static class AccessClaims
{
    public const string Subject = "sub";

    /// <summary>The signed-in person's subject.</summary>
    public static string UserId(ClaimsPrincipal principal) =>
        principal.FindFirstValue(Subject) ?? throw new InvalidOperationException("Authenticated user id is missing.");
}
