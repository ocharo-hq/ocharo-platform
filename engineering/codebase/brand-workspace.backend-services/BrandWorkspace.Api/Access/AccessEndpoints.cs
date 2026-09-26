using System.Security.Claims;
using System.Text.RegularExpressions;
using BrandWorkspace.Application.Access;
using BrandWorkspace.Persistence.Access;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BrandWorkspace.Api.Access;

/// <summary>Exposes operator sessions and authenticated organization creation.</summary>
public static class AccessEndpoints
{
    private static readonly AccessUser TimingUser = new();
    private static readonly string TimingHash = new PasswordHasher<AccessUser>().HashPassword(TimingUser, Guid.NewGuid().ToString());

    public static WebApplication MapAccessEndpoints(this WebApplication app)
    {
        app.MapGet("/api/auth/session", async (HttpContext http, UserManager<AccessUser> users,
            IAntiforgery antiforgery, CancellationToken token) =>
        {
            var user = http.User.Identity?.IsAuthenticated == true
                ? await users.FindByIdAsync(AccessClaims.UserId(http.User)) : null;
            var csrf = antiforgery.GetAndStoreTokens(http);
            return Results.Ok(new { data = new
            {
                authenticated = user is not null,
                user = user is null ? null : new { user.Id, email = user.Email, user.DisplayName },
                sessionId = AccessClaims.Session(http.User), csrfToken = csrf.RequestToken,
                setupRequired = !await users.Users.AnyAsync(token)
            }});
        }).AllowAnonymous();
        app.MapPost("/api/auth/login", LoginAsync).AllowAnonymous();
        app.MapPost("/api/auth/logout", async (HttpContext http, IAccessRepository repository, TimeProvider clock) =>
        {
            if (AccessClaims.Session(http.User) is { } session)
                await repository.RevokeSessionAsync(session, clock.GetUtcNow(), http.RequestAborted);
            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.NoContent();
        });
        app.MapGet("/api/organizations", async (HttpContext http, IAccessRepository repository) =>
            Results.Ok(new { data = await repository.ListOrganizationsAsync(AccessClaims.UserId(http.User), http.RequestAborted) }));
        app.MapPost("/api/organizations", async (OrganizationInput input, HttpContext http, IAccessRepository repository) =>
        {
            var name = input.Name?.Trim();
            var slug = input.Slug?.Trim();
            if (string.IsNullOrEmpty(name) || name.Length > 120 || name.Any(char.IsControl)
                || string.IsNullOrEmpty(slug) || slug.Length > 64
                || !Regex.IsMatch(slug, "^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)))
                return AccessProblems.Create(400, "Use a name of 1–120 characters and a lowercase alphanumeric slug", "invalid_organization");
            var organization = await repository.CreateOrganizationAsync(name, slug, AccessClaims.UserId(http.User), http.RequestAborted);
            return organization is null ? AccessProblems.Create(409, "Organization slug is already in use", "organization_conflict")
                : Results.Created($"/api/organizations/{organization.Id}", new { data = organization });
        });
        return app;
    }

    private static async Task<IResult> LoginAsync(LoginInput input, HttpContext http, UserManager<AccessUser> users,
        SignInManager<AccessUser> signIn, IPasswordHasher<AccessUser> hasher, IAccessRepository repository, TimeProvider clock)
    {
        if (string.IsNullOrWhiteSpace(input.Email) || input.Email.Length > 254 || string.IsNullOrEmpty(input.Password)
            || input.Password.Length > 1024)
            return AccessProblems.Create(401, "Email or password is incorrect", "invalid_credentials");
        var user = await users.FindByEmailAsync(input.Email.Trim());
        if (user is null || !user.IsActive || user.TwoFactorEnabled)
        {
            _ = hasher.VerifyHashedPassword(TimingUser, TimingHash, input.Password);
            return AccessProblems.Create(401, "Email or password is incorrect", "invalid_credentials");
        }
        var check = await signIn.CheckPasswordSignInAsync(user, input.Password, lockoutOnFailure: true);
        if (!check.Succeeded) return AccessProblems.Create(401, "Email or password is incorrect", "invalid_credentials");
        var now = clock.GetUtcNow();
        var session = Guid.NewGuid();
        var expires = now.AddHours(8);
        await repository.CreateSessionAsync(session, user.Id, await users.GetSecurityStampAsync(user), expires,
            AccessClaims.Session(http.User), http.RequestAborted);
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id), new Claim(ClaimTypes.Name, user.Email!),
            new Claim(AccessClaims.SessionId, session.ToString("D"))
        }, CookieAuthenticationDefaults.AuthenticationScheme);
        await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = false, IssuedUtc = now, ExpiresUtc = expires, AllowRefresh = false });
        return Results.Ok(new { data = new { authenticated = true } });
    }

    public sealed record LoginInput(string? Email, string? Password);
    public sealed record OrganizationInput(string? Name, string? Slug);
}
