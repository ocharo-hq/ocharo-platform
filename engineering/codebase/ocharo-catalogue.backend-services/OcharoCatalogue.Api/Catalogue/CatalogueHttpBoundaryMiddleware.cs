using Microsoft.Extensions.Options;
using OcharoCatalogue.Infrastructure.Catalogue;

namespace OcharoCatalogue.Api.Catalogue;

/// <summary>
/// Sets the catalogue's response headers and refuses cross-origin writes before sign-in runs: the private API is
/// <c>no-store</c>, and the read-only public feed and media routes stay readable cross-origin so sibling Ocharo apps
/// (Marketing, Blog) can consume them; <c>nosniff</c> and the other secure headers are the SDK pipeline's. A write is
/// accepted from the host's own origin and from <see cref="CatalogueOptions.TrustedOrigins"/>. Who may call what is the
/// Identity sign-in's (<see cref="Access.CatalogueAccess"/>).
/// </summary>
public sealed class CatalogueHttpBoundaryMiddleware(RequestDelegate next, IOptions<CatalogueOptions> options)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path;
        if (path.StartsWithSegments("/api/public") || path.StartsWithSegments("/api/catalogue/media"))
            context.Response.Headers.AccessControlAllowOrigin = "*";
        else if (path.StartsWithSegments("/api"))
            context.Response.Headers.CacheControl = "no-store";

        var method = context.Request.Method;
        if (HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsPatch(method) || HttpMethods.IsDelete(method))
        {
            var origin = context.Request.Headers.Origin.ToString();
            var fetchSite = context.Request.Headers["Sec-Fetch-Site"].ToString();
            if (origin.Length > 0 ? !IsAllowedOrigin(origin, context.Request) : fetchSite == "cross-site")
            {
                await Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Cross-origin write refused",
                    detail: "Cross-origin writes are disabled.",
                    extensions: new Dictionary<string, object?> { ["code"] = "cross_origin_write" }).ExecuteAsync(context);
                return;
            }
        }

        await next(context);
    }

    private bool IsAllowedOrigin(string origin, HttpRequest request)
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || uri.AbsolutePath != "/")
            return false;
        var authority = uri.GetLeftPart(UriPartial.Authority);
        if (string.Equals(authority, $"{request.Scheme}://{request.Host.Value}", StringComparison.OrdinalIgnoreCase)) return true;
        return options.Value.TrustedOrigins.Contains(authority, StringComparer.OrdinalIgnoreCase);
    }
}
