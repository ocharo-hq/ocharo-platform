using Microsoft.Extensions.Options;
using OcharoCatalogue.Api.Catalogue.Core.Errors;
using OcharoCatalogue.Api.Catalogue.Core.Extensions;
using OcharoCatalogue.Infrastructure.Catalogue.Core.Options;

namespace OcharoCatalogue.Api.Catalogue.Core.Middlewares;

/// <summary>
/// Sets the catalogue's response headers and refuses cross-origin writes before sign-in runs: the private API is
/// <c>no-store</c>, and the read-only public feed and media routes stay readable cross-origin so sibling Ocharo apps
/// (Marketing, Blog) can consume them; <c>nosniff</c> and the other secure headers are the SDK pipeline's. A write is
/// accepted from the host's own origin and from <see cref="CatalogueOptions.TrustedOrigins"/>; a refused one answers
/// the
/// SDK's problem details. Who may call what is the Identity sign-in's (<see cref="Access.CatalogueAccess"/>).
/// </summary>
public sealed class CatalogueHttpBoundaryMiddleware(RequestDelegate next, IOptions<CatalogueOptions> options)
{
    /// <summary>Sets the headers, then refuses a cross-origin write or passes the request on.</summary>
    /// <param name="context">The request.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path;
        if (path.StartsWithSegments("/api/public") || path.StartsWithSegments("/api/catalogue/media"))
        {
            context.Response.Headers.AccessControlAllowOrigin = "*";
        }
        else if (path.StartsWithSegments("/api"))
        {
            context.Response.Headers.CacheControl = "no-store";
        }

        if (IsWrite(context.Request.Method) && !IsSameOrTrustedWrite(context.Request))
        {
            await context.WriteProblemAsync(CatalogueHttpErrors.CrossOriginWrite());
            return;
        }

        await next(context);
    }

    private static bool IsWrite(string method)
    {
        return HttpMethods.IsPost(method)
            || HttpMethods.IsPut(method)
            || HttpMethods.IsPatch(method)
            || HttpMethods.IsDelete(method);
    }

    private bool IsSameOrTrustedWrite(HttpRequest request)
    {
        var origin = request.Headers.Origin.ToString();
        if (origin.Length > 0)
        {
            return IsAllowedOrigin(origin, request);
        }

        return request.Headers["Sec-Fetch-Site"].ToString() != "cross-site";
    }

    private bool IsAllowedOrigin(string origin, HttpRequest request)
    {
        var parsed = Uri.TryCreate(
            origin,
            UriKind.Absolute,
            out var uri);
        if (!parsed || uri!.Scheme != Uri.UriSchemeHttps || uri.AbsolutePath != "/")
        {
            return false;
        }

        var authority = uri.GetLeftPart(UriPartial.Authority);
        if (string.Equals(
            authority,
            $"{request.Scheme}://{request.Host.Value}",
            StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return options.Value.TrustedOrigins.Contains(authority, StringComparer.OrdinalIgnoreCase);
    }
}
