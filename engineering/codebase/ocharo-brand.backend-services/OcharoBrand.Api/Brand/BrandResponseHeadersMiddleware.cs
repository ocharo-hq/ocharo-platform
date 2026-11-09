namespace OcharoBrand.Api.Brand;

/// <summary>
/// Sets Brand's own response headers on the API: <c>no-store</c> (an endpoint may set its own caching), and
/// <c>Access-Control-Allow-Origin: *</c> on the read-only public feed and media, so every Ocharo app (Catalogue,
/// Marketing, Blog, Studio) can consume them. The SDK's secure headers send <c>nosniff</c> on every response.
/// </summary>
public sealed class BrandResponseHeadersMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path;
        if (path.StartsWithSegments("/api/public"))
            context.Response.Headers.AccessControlAllowOrigin = "*";
        else if (path.StartsWithSegments("/api"))
            context.Response.Headers.CacheControl = "no-store";
        return next(context);
    }
}
