using Ocharo.Sso;
using System.Net;

namespace OcharoBrand.Api.Brand;

/// <summary>
/// Keeps the unauthenticated pilot on loopback unless it is public (<see cref="IsPublic"/>), and rejects cross-origin
/// writes. The read-only public feed and media routes stay readable cross-origin so every Ocharo app (Catalogue, Marketing, Blog, Studio) can consume them locally.
/// </summary>
public sealed class LocalPilotBoundaryMiddleware(RequestDelegate next, IConfiguration configuration)
{
    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        var path = context.Request.Path;
        if (path.StartsWithSegments("/api/public"))
            context.Response.Headers.AccessControlAllowOrigin = "*";
        else if (path.StartsWithSegments("/api"))
            context.Response.Headers.CacheControl = "no-store";
        if (context.RequestServices.GetRequiredService<SsoOptions>().Enabled)
        {
            await next(context);
            return;
        }
        var address = context.Connection.RemoteIpAddress;
        var localHost = context.Request.Host.Host is "localhost" or "127.0.0.1" or "[::1]" or "::1";
        if (!IsPublic(configuration) && (!localHost || address is not null && !IPAddress.IsLoopback(address)))
        {
            await RejectAsync(context, "This pilot accepts loopback requests only.");
            return;
        }

        var method = context.Request.Method;
        if (HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsPatch(method) || HttpMethods.IsDelete(method))
        {
            var origin = context.Request.Headers.Origin.ToString();
            var fetchSite = context.Request.Headers["Sec-Fetch-Site"].ToString();
            if (origin.Length > 0 ? !IsAllowedOrigin(origin, context.Request) : fetchSite == "cross-site")
            {
                await RejectAsync(context, "Cross-origin writes are disabled.");
                return;
            }
        }

        await next(context);
    }

    /// <summary>
    /// Whether any caller may reach the pilot without sign-in. The owner opened the deployed pilots to the public on
    /// 2026-10-24, until the Ocharo identity server lands: <c>Brand:PublicAccess</c> decides, and unset it is on for a
    /// deployed host (<c>DEPLOY_ENVIRONMENT</c>) and off for a local run, which keeps the loopback boundary.
    /// </summary>
    public static bool IsPublic(IConfiguration configuration) =>
        configuration.GetValue<bool?>("Brand:PublicAccess")
        ?? !string.IsNullOrWhiteSpace(configuration["DEPLOY_ENVIRONMENT"] ?? Environment.GetEnvironmentVariable("DEPLOY_ENVIRONMENT"));

    private bool IsAllowedOrigin(string origin, HttpRequest request)
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || uri.AbsolutePath != "/")
            return false;
        var authority = uri.GetLeftPart(UriPartial.Authority);
        if (string.Equals(authority, $"{request.Scheme}://{request.Host.Value}", StringComparison.OrdinalIgnoreCase)) return true;
        var trusted = configuration.GetSection("Brand:TrustedOrigins").Get<string[]>() ?? [];
        return trusted.Contains(authority, StringComparer.OrdinalIgnoreCase);
    }

    private static Task RejectAsync(HttpContext context, string detail) =>
        Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Local pilot boundary", detail: detail).ExecuteAsync(context);
}
