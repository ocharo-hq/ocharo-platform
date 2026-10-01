using System.Net;

namespace OcharoCatalogue.Api.Catalogue;

/// <summary>
/// Keeps the unauthenticated pilot on loopback and rejects cross-origin writes. The read-only public feed and
/// media routes stay readable cross-origin so sibling Ocharo apps (Marketing, Blog) can consume them locally.
/// </summary>
public sealed class LocalPilotBoundaryMiddleware(RequestDelegate next, IConfiguration configuration)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var address = context.Connection.RemoteIpAddress;
        var localHost = context.Request.Host.Host is "localhost" or "127.0.0.1" or "[::1]" or "::1";
        if (!localHost || address is not null && !IPAddress.IsLoopback(address))
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

        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        var path = context.Request.Path;
        if (path.StartsWithSegments("/api/public") || path.StartsWithSegments("/api/catalogue/media"))
            context.Response.Headers.AccessControlAllowOrigin = "*";
        else if (path.StartsWithSegments("/api"))
            context.Response.Headers.CacheControl = "no-store";
        await next(context);
    }

    private bool IsAllowedOrigin(string origin, HttpRequest request)
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") || uri.AbsolutePath != "/")
            return false;
        var authority = uri.GetLeftPart(UriPartial.Authority);
        if (string.Equals(authority, $"{request.Scheme}://{request.Host.Value}", StringComparison.OrdinalIgnoreCase)) return true;
        var trusted = configuration.GetSection("Catalogue:TrustedOrigins").Get<string[]>() ?? [];
        return trusted.Contains(authority, StringComparer.OrdinalIgnoreCase);
    }

    private static Task RejectAsync(HttpContext context, string detail) =>
        Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Local pilot boundary", detail: detail).ExecuteAsync(context);
}
