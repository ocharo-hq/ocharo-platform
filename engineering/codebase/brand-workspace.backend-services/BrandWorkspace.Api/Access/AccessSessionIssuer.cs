using System.Security.Claims;
using BrandWorkspace.Application.Access;
using BrandWorkspace.Persistence.Access;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace BrandWorkspace.Api.Access;

/// <summary>Issues the same durable operator session for every supported sign-in method.</summary>
public static class AccessSessionIssuer
{
    public static async Task IssueAsync(HttpContext http, AccessUser user, IAccessRepository repository, TimeProvider clock)
    {
        var ticket = Create(user, clock.GetUtcNow());
        await repository.CreateSessionAsync(ticket.Id, user.Id, user.SecurityStamp!, ticket.ExpiresAt,
            AccessClaims.Session(http.User), http.RequestAborted);
        await WriteCookieAsync(http, user, ticket);
    }

    public static AccessSessionTicket Create(AccessUser user, DateTimeOffset now)
    {
        if (!user.IsActive || string.IsNullOrEmpty(user.SecurityStamp)) throw new InvalidOperationException("An active operator is required.");
        return new AccessSessionTicket(Guid.NewGuid(), now, now.AddHours(8));
    }

    public static Task WriteCookieAsync(HttpContext http, AccessUser user, AccessSessionTicket ticket)
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id), new Claim(ClaimTypes.Name, user.Email!),
            new Claim(AccessClaims.SessionId, ticket.Id.ToString("D"))
        }, CookieAuthenticationDefaults.AuthenticationScheme);
        return http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = false, IssuedUtc = ticket.IssuedAt, ExpiresUtc = ticket.ExpiresAt, AllowRefresh = false });
    }
}

public sealed record AccessSessionTicket(Guid Id, DateTimeOffset IssuedAt, DateTimeOffset ExpiresAt);
