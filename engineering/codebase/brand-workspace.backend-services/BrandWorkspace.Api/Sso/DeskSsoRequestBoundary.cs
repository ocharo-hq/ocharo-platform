using Microsoft.AspNetCore.Http.Features;

namespace BrandWorkspace.Api.Sso;

/// <summary>Limits request allocation before JSON binding and protects response caching.</summary>
public sealed class DeskSsoRequestBoundary(RequestDelegate next)
{
    public const long MaximumBodyBytes = 16 * 1024;

    public async Task InvokeAsync(HttpContext http)
    {
        if (!http.Request.Path.StartsWithSegments("/api/sso"))
        {
            await next(http);
            return;
        }
        http.Response.Headers.CacheControl = "no-store";
        http.Response.Headers["Referrer-Policy"] = "no-referrer";
        if (http.Request.ContentLength > MaximumBodyBytes)
        {
            await Results.Problem(statusCode: 413, title: "Delegation request is too large").ExecuteAsync(http);
            return;
        }
        if (http.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
            limit.MaxRequestBodySize = MaximumBodyBytes;
        await next(http);
    }
}
