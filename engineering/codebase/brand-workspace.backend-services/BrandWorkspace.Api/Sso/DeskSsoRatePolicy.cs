using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace BrandWorkspace.Api.Sso;

/// <summary>Bounds credential attempts without sharing Telegram's rejection contract.</summary>
public sealed class DeskSsoRatePolicy : IRateLimiterPolicy<string>
{
    public const string Name = "desk-sso";

    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected
    {
        get { return RejectAsync; }
    }

    public RateLimitPartition<string> GetPartition(HttpContext http)
    {
        var refresh = http.Request.Path == "/api/sso/refresh" || http.Request.Path == "/api/sso/revoke";
        var key = $"{(refresh ? "refresh" : "authorize")}:{http.Connection.RemoteIpAddress}";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = refresh ? 120 : 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0
        });
    }

    private static async ValueTask RejectAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        context.HttpContext.Response.Headers.CacheControl = "no-store";
        context.HttpContext.Response.Headers["Referrer-Policy"] = "no-referrer";
        context.HttpContext.Response.Headers.RetryAfter = "60";
        await Results.Problem(statusCode: 429, title: "Wait before trying again").ExecuteAsync(context.HttpContext);
    }
}
