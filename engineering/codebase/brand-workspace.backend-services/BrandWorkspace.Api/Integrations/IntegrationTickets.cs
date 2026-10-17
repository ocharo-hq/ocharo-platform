using System.Security.Claims;
using System.Text;
using BrandWorkspace.Api.Access;
using BrandWorkspace.Api.Preparation;
using BrandWorkspace.Application.Access;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using WoW.Two.Sdk.Beta.Be.Identity.Jwt.Issuance;

namespace BrandWorkspace.Api.Integrations;

/// <summary>Short-lived delegated access. Platform sessions and live memberships remain authoritative.</summary>
public sealed class IntegrationTickets(IConfiguration configuration, TimeProvider time, IAccessRepository access)
{
    private const string Issuer = "ocharo-platform/integration-v1";
    public static bool ValidAudience(string? audience) => audience is "ocharo-studio" or "ocharo-marketing" or "ocharo-desk-mcp"
        or "ocharo-catalogue" or "ocharo-brand" or "ocharo-editorial" or "ocharo-assets";
    private string? SigningKey => configuration["Integrations:SigningKey"] is { } key && Encoding.UTF8.GetByteCount(key) >= 32 ? key : null;
    /// <summary>Reports whether delegated access can be signed, without exposing its key.</summary>
    public bool IsConfigured => SigningKey is not null;
    public IssuedIntegrationTicket? Issue(string audience, OrganizationAccess member, Guid sessionId)
    {
        if (SigningKey is not { } key) return null;
        ITokenIssuer issuer = new JwtTokenIssuer(new JwtTokenIssuerOptions
        { Issuer = Issuer, Audience = audience, SigningKey = key, Lifetime = TimeSpan.FromMinutes(5) }, time);
        var token = issuer.Issue([new Claim("sub", member.UserId), new Claim("org", member.OrganizationId.ToString()),
            new Claim("sid", sessionId.ToString()), new Claim("jti", Guid.NewGuid().ToString())]);
        return new(token, time.GetUtcNow().AddMinutes(5));
    }
    public async Task<object> IntrospectAsync(string? token, string? audience, CancellationToken cancellation)
    {
        var result = await ValidateAsync(token, audience, cancellation);
        if (result is null) return new { active = false };
        var member = result.Membership;
        return new { active = true, userId = member.UserId, organizationId = member.OrganizationId, role = member.Role,
            canEdit = member.CanEdit, canApprove = member.CanApprove, expiresAt = result.ExpiresAt };
    }

    /// <summary>Resolves a trusted live membership from a bounded audience-specific delegated ticket.</summary>
    public async Task<IntegrationAccess?> ValidateAsync(string? token, string? audience, CancellationToken cancellation)
    {
        IntegrationAccess? inactive = null;
        if (!ValidAudience(audience) || string.IsNullOrWhiteSpace(token) || token.Length > 8192 || SigningKey is not { } key) return inactive;
        var result = await new JsonWebTokenHandler { MapInboundClaims = false }.ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256], ValidateIssuer = true, ValidIssuer = Issuer,
            ValidateAudience = true, ValidAudience = audience, ValidateLifetime = true, RequireExpirationTime = true,
            ClockSkew = TimeSpan.Zero,
            LifetimeValidator = (notBefore, expires, _, _) => expires is not null && notBefore is not null
                && notBefore <= time.GetUtcNow().UtcDateTime && expires > time.GetUtcNow().UtcDateTime
                && expires - notBefore <= TimeSpan.FromMinutes(5)
        });
        if (!result.IsValid || result.SecurityToken is not JsonWebToken jwt) return inactive;
        var user = jwt.TryGetClaim("sub", out var userClaim) ? userClaim.Value : null;
        if (string.IsNullOrEmpty(user) || !jwt.TryGetClaim("org", out var orgClaim) || !Guid.TryParse(orgClaim.Value, out var organization)
            || !jwt.TryGetClaim("sid", out var sessionClaim) || !Guid.TryParse(sessionClaim.Value, out var session)) return inactive;
        if (!await access.IsSessionActiveAsync(session, user, time.GetUtcNow(), cancellation)) return inactive;
        var membership = await access.FindMembershipAsync(organization, user, cancellation);
        if (membership is null) return inactive;
        return new(membership, new DateTimeOffset(jwt.ValidTo, TimeSpan.Zero));
    }
}

/// <summary>Trusted delegated organization authority and its expiration.</summary>
public sealed record IntegrationAccess(OrganizationAccess Membership, DateTimeOffset ExpiresAt);

public sealed record TicketRequest(string Audience);
public sealed record IntrospectionRequest(string Token, string Audience);

public static class IntegrationEndpoints
{
    public static WebApplication MapIntegrationEndpoints(this WebApplication app)
    {
        app.MapPost("/api/organizations/{organizationId:guid}/integration-tickets",
            (TicketRequest request, HttpContext http, IntegrationTickets tickets) =>
            {
                http.Response.Headers.CacheControl = "no-store";
                if (!IntegrationTickets.ValidAudience(request.Audience)) return Results.Problem(statusCode: 400, title: "Unknown integration audience");
                var session = AccessClaims.Session(http.User);
                if (session is null) return Results.Unauthorized();
                var issued = tickets.Issue(request.Audience, OrganizationAccessFilter.GetAccess(http), session.Value);
                return issued is null ? Results.Problem(statusCode: 503, title: "Integration signing key is not configured") : Results.Ok(new { data = issued });
            }).RequireAuthorization().AddEndpointFilter<OrganizationAccessFilter>();
        app.MapPost("/api/integrations/introspect", async (IntrospectionRequest request, HttpContext http,
            IntegrationTickets tickets, CancellationToken token) =>
            {
                http.Response.Headers.CacheControl = "no-store";
                if (!http.Request.IsHttps) return Results.Problem(statusCode: 400, title: "HTTPS is required");
                return Results.Ok(new { data = await tickets.IntrospectAsync(request.Token, request.Audience, token) });
            }).WithMetadata(new SkipAntiforgeryValidation()).AllowAnonymous();
        return app;
    }
}

/// <summary>Bounded delegated access; JSON names preserve the existing integration contract.</summary>
public sealed record IssuedIntegrationTicket(
    [property: System.Text.Json.Serialization.JsonPropertyName("token")] string Token,
    [property: System.Text.Json.Serialization.JsonPropertyName("expiresAt")] DateTimeOffset ExpiresAt);
