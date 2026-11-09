using Microsoft.AspNetCore.Mvc.Filters;

namespace BrandWorkspace.Api.Foundation.Filters;

/// <summary>Marks every answer of an action <c>Cache-Control: no-store</c>, its problems included.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class NoStoreAttribute : Attribute, IResourceFilter
{
    /// <inheritdoc />
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.HttpContext.Response.Headers.CacheControl = "no-store";
    }

    /// <inheritdoc />
    public void OnResourceExecuted(ResourceExecutedContext context)
    {
    }
}
