using BrandWorkspace.Application.Preparation.Models;

namespace BrandWorkspace.Api.Preparation;

/// <summary>Maps bounded preparation failures and prevents caching of workspace responses.</summary>
public sealed class PreparationErrorFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        context.HttpContext.Response.Headers.CacheControl = "no-store";
        try { return await next(context); }
        catch (PreparationException exception)
        {
            var status = exception.Code switch
            {
                "not-found" => 404,
                "conflict" => 409,
                "forbidden" => 403,
                "too-large" => 413,
                _ => 400
            };
            return Results.Problem(statusCode: status, title: exception.Message,
                extensions: new Dictionary<string, object?> { ["code"] = exception.Code });
        }
    }
}
