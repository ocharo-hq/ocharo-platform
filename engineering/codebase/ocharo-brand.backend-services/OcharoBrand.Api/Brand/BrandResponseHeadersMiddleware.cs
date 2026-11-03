namespace OcharoBrand.Api.Brand;

/// <summary>
/// Sets Brand's response headers: <c>nosniff</c> on every response, <c>no-store</c> on the API (an endpoint may set
/// its own caching), and <c>Access-Control-Allow-Origin: *</c> on the read-only public feed and media, so every Ocharo app
/// (Catalogue, Marketing, Blog, Studio) can consume them.
/// </summary>
public sealed class BrandResponseHeadersMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        context.Response.Headers.XContentTypeOptions = "nosniff";
        var path = context.Request.Path;
        if (path.StartsWithSegments("/api/public"))
            context.Response.Headers.AccessControlAllowOrigin = "*";
        else if (path.StartsWithSegments("/api"))
            context.Response.Headers.CacheControl = "no-store";
        return next(context);
    }
}
