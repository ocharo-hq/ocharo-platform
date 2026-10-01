using Microsoft.Data.Sqlite;
using OcharoCatalogue.Application.Catalogue;

namespace OcharoCatalogue.Api.Catalogue;

/// <summary>Maps rejected catalogue commands to ProblemDetails responses.</summary>
public sealed class CatalogueExceptionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try { await next(context); }
        catch (CatalogueException exception) when (!context.Response.HasStarted)
        {
            await Results.Problem(statusCode: exception.StatusCode, title: "Catalogue command rejected", detail: exception.Message).ExecuteAsync(context);
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode is 5 or 6 && !context.Response.HasStarted)
        {
            await Results.Problem(statusCode: 409, title: "Catalogue busy", detail: "Another save is finishing. Reload and retry.").ExecuteAsync(context);
        }
    }
}
