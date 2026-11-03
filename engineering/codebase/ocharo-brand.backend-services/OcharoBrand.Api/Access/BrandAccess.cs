namespace OcharoBrand.Api.Access;

/// <summary>Which requests Brand answers without a session, and the permissions its private API checks.</summary>
public static class BrandAccess
{
    /// <summary>The permission unsafe requests of the private API need.</summary>
    public const string EditPermission = "brand:edit";

    /// <summary>The permission publication (<c>…/publish</c>, <c>…/withdraw</c>) needs, beside <see cref="EditPermission"/>.</summary>
    public const string ApprovePermission = "brand:approve";

    /// <summary>
    /// Whether a request is public: a read (GET, HEAD) of the kit every Ocharo app consumes (<c>/api/public</c>), the
    /// runtime config, the status and version routes or the health probe. Static files and the SPA fallback are anonymous
    /// endpoints of their own; everything else under <c>/api</c> but the session routes needs a member's session.
    /// </summary>
    public static bool IsPublic(HttpContext http)
    {
        if (!HttpMethods.IsGet(http.Request.Method) && !HttpMethods.IsHead(http.Request.Method)) return false;
        var path = http.Request.Path;
        return path.StartsWithSegments("/api/public") || path == "/health"
            || path == "/api/system/version" || path == "/api/system/status" || path == "/api/runtime-config";
    }

    /// <summary>Whether a request publishes or withdraws, so it needs <see cref="ApprovePermission"/>.</summary>
    public static bool IsPublication(HttpRequest request)
    {
        var path = (request.Path.Value ?? "").TrimEnd('/');
        return HttpMethods.IsPost(request.Method)
            && (path.EndsWith("/publish", StringComparison.OrdinalIgnoreCase) || path.EndsWith("/withdraw", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Whether a method may change state.</summary>
    public static bool IsUnsafe(string method) =>
        !(HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method));
}
