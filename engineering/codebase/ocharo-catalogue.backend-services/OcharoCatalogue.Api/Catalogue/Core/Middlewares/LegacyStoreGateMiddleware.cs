using OcharoCatalogue.Api.Catalogue.Core.Errors;
using OcharoCatalogue.Api.Catalogue.Core.Extensions;
using OcharoCatalogue.Persistence.Import;

namespace OcharoCatalogue.Api.Catalogue.Core.Middlewares;

/// <summary>Answers every catalogue route, the published reader's included, 503 while the retired SQLite store waits for
/// <c>import-sqlite</c>, so no write lands in PostgreSQL before the copy and no reader sees a half-empty catalogue.</summary>
/// <remarks>Sign-in, runtime config, status and health stay open. Once the file is retired it never returns, so the
/// gate stops looking for it.</remarks>
public sealed class LegacyStoreGateMiddleware(RequestDelegate next, LegacySqliteSettings legacy)
{
    private volatile bool retired;

    public async Task InvokeAsync(HttpContext context)
    {
        if (!retired && Gated(context.Request.Path))
        {
            if (legacy.IsWaiting)
            {
                await context.WriteProblemAsync(CatalogueHttpErrors.StorageMoving());
                return;
            }

            retired = true;
        }

        await next(context);
    }

    private static bool Gated(PathString path) =>
        path.StartsWithSegments("/api/catalogue") || path.StartsWithSegments("/api/public");
}
