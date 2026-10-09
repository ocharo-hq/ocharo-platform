using Ocharo.Sso;

namespace OcharoBrand.Api.Configurations;

public static partial class HostConfiguration
{
    private static bool IsPublicSsoRequest(HttpContext http)
    {
        if (!HttpMethods.IsGet(http.Request.Method) && !HttpMethods.IsHead(http.Request.Method)) return false;
        var path = http.Request.Path;
        return path.StartsWithSegments("/api/public") || path == "/health" || path == "/api/system/version";
    }

    private static Task RequirePublicationApproval(HttpContext http, RequestDelegate next)
    {
        var path = (http.Request.Path.Value ?? "").TrimEnd('/');
        if (http.RequestServices.GetRequiredService<SsoOptions>().Enabled && HttpMethods.IsPost(http.Request.Method)
            && (path.EndsWith("/publish", StringComparison.OrdinalIgnoreCase) || path.EndsWith("/withdraw", StringComparison.OrdinalIgnoreCase))
            && http.User.FindFirst("ocharo:can-approve")?.Value != "true")
            return Results.Problem(statusCode: 403, title: "Publication approval access is required",
                extensions: new Dictionary<string, object?> { ["code"] = "sso_approval_required" }).ExecuteAsync(http);
        return next(http);
    }
}
