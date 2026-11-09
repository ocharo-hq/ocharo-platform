using OcharoBrand.Api.Access.Extensions;
using OcharoBrand.Application.Brand.Errors;
using OcharoBrand.Persistence.Import;

namespace OcharoBrand.Api.Brand;

/// <summary>Answers every brand route — the private API and the public kit, its logos, media and tokens — 503 while the
/// retired SQLite store waits for <c>import-sqlite</c>, so nothing reads an empty PostgreSQL store or writes into it before
/// the copy. Runs after access, so a private route still asks for a session first.</summary>
public sealed class BrandStorageMovingMiddleware(RequestDelegate next, LegacySqliteSettings legacy)
{
    public Task InvokeAsync(HttpContext http)
    {
        var path = http.Request.Path;
        if ((path.StartsWithSegments("/api/brand") || path.StartsWithSegments("/api/public/brand")) && legacy.Waiting)
        {
            http.Response.Headers.RetryAfter = "60";
            return http.WriteProblemAsync(BrandErrors.StorageMoving());
        }

        return next(http);
    }
}
