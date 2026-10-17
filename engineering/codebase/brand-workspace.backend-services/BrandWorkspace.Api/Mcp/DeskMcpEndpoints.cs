using BrandWorkspace.Api.Access;
using BrandWorkspace.Api.Integrations;
using WoW.Two.Sdk.Beta.Be.Ai.Mcp;

namespace BrandWorkspace.Api.Mcp;

/// <summary>Maps explicit read-only MCP access and its organization-scoped connection metadata.</summary>
public static class DeskMcpEndpoints
{
    public const string Policy = "DeskMcpRead";
    public const string Path = "/mcp";

    /// <summary>Publishes connection metadata while leaving the protocol endpoint disabled unless opted in.</summary>
    public static WebApplication MapDeskMcpEndpoints(this WebApplication app)
    {
        var settings = app.Services.GetRequiredService<DeskMcpSettings>();
        app.MapGet("/api/organizations/{organizationId:guid}/integrations/mcp", (IntegrationTickets tickets) => Results.Ok(new
        {
            data = new
            {
                enabled = settings.Enabled && tickets.IsConfigured,
                disabledReason = !settings.Enabled ? "disabled" : !tickets.IsConfigured ? "signing-key-missing" : null,
                endpoint = Path,
                audience = DeskMcpAuthenticationHandler.Audience,
                tokenLifetimeSeconds = 300,
                readOnly = true
            }
        })).RequireAuthorization().AddEndpointFilter<OrganizationAccessFilter>();

        if (!settings.Enabled)
        {
            app.Map(Path, () => Results.Problem(statusCode: 404, title: "Not Found")).AllowAnonymous();
            return app;
        }

        app.MapAuthenticatedMcp(Path, Policy);
        return app;
    }

    /// <summary>Rejects insecure transport and cross-origin browser access before the protocol executes.</summary>
    public static WebApplication UseDeskMcpBoundary(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            if (!context.Request.Path.StartsWithSegments(Path, StringComparison.OrdinalIgnoreCase))
            {
                await next(context);
                return;
            }
            context.Response.Headers.CacheControl = "no-store";
            if (!context.Request.IsHttps)
            {
                await AccessProblems.Create(400, "HTTPS is required", "https_required").ExecuteAsync(context);
                return;
            }
            var origins = context.Request.Headers.Origin;
            if (origins.Count > 0 && (origins.Count != 1
                || !string.Equals(origins[0], $"{context.Request.Scheme}://{context.Request.Host}", StringComparison.OrdinalIgnoreCase)))
            {
                await AccessProblems.Create(403, "The request origin is not allowed", "mcp_origin_forbidden").ExecuteAsync(context);
                return;
            }
            await next(context);
        });
        return app;
    }
}
