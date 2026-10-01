using Microsoft.Data.Sqlite;
using OcharoBrand.Application.Brand;

namespace OcharoBrand.Api.Brand;

/// <summary>Maps rejected brand commands to ProblemDetails responses.</summary>
public sealed class BrandExceptionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try { await next(context); }
        catch (BrandException exception) when (!context.Response.HasStarted)
        {
            await Results.Problem(statusCode: exception.StatusCode, title: "Brand command rejected", detail: exception.Message).ExecuteAsync(context);
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode is 5 or 6 && !context.Response.HasStarted)
        {
            await Results.Problem(statusCode: 409, title: "Brand busy", detail: "Another save is finishing. Reload and retry.").ExecuteAsync(context);
        }
    }
}
