using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using BrandWorkspace.Api.Access;
using BrandWorkspace.Api.Integrations;
using BrandWorkspace.Application.Access;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace BrandWorkspace.Api.Mcp;

/// <summary>Authenticates short-lived MCP tickets against live Desk membership and session authority.</summary>
public sealed class DeskMcpAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IntegrationTickets tickets) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "DeskMcpTicket";
    public const string Audience = "ocharo-desk-mcp";
    private static readonly object MembershipKey = new();

    /// <summary>Requires one audience-bound bearer ticket; browser cookies never grant MCP access.</summary>
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var values = Request.Headers.Authorization;
        if (values.Count == 0) return AuthenticateResult.NoResult();
        if (!Request.IsHttps || values.Count != 1
            || !AuthenticationHeaderValue.TryParse(values[0], out var header)
            || !string.Equals(header.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase))
            return AuthenticateResult.Fail("A Desk MCP bearer ticket is required.");

        IntegrationAccess? access = await tickets.ValidateAsync(header.Parameter, Audience, Context.RequestAborted);
        if (access is null) return AuthenticateResult.Fail("The Desk MCP ticket is inactive.");
        Context.Items[MembershipKey] = access.Membership;
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, access.Membership.UserId)], SchemeName);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }

    /// <summary>Returns a structured authentication failure without redirecting to a login page.</summary>
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers.WWWAuthenticate = "Bearer";
        return AccessProblems.Create(401, "A current Desk MCP ticket is required", "mcp_authentication_required")
            .ExecuteAsync(Context);
    }

    /// <summary>Reads only the membership verified by this request's bearer authentication.</summary>
    public static OrganizationAccess GetAccess(HttpContext context)
    {
        if (context.User.Identities.Any(identity => identity.IsAuthenticated && identity.AuthenticationType == SchemeName)
            && context.Items.TryGetValue(MembershipKey, out object? value) && value is OrganizationAccess access)
            return access;
        throw new InvalidOperationException("Desk MCP authentication must complete before a tool executes.");
    }
}
