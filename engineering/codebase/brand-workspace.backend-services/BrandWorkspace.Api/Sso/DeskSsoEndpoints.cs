using BrandWorkspace.Api.Access;
using BrandWorkspace.Application.Sso;
using BrandWorkspace.Persistence.Sso;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace BrandWorkspace.Api.Sso;

/// <summary>Internal, explicitly configured confidential-client delegation; not an OAuth/OIDC issuer.</summary>
public static class DeskSsoEndpoints
{
    public static WebApplicationBuilder AddDeskSso(this WebApplicationBuilder builder)
    {
        builder.Services.AddOptions<DeskSsoOptions>().Bind(builder.Configuration.GetSection("Sso")).ValidateOnStart();
        builder.Services.AddSingleton<IValidateOptions<DeskSsoOptions>, DeskSsoOptionsValidator>();
        builder.Services.AddRateLimiter(options => options.AddPolicy<string, DeskSsoRatePolicy>(DeskSsoRatePolicy.Name));
        builder.Services.AddSingleton<ISsoGrantStore, SsoGrantStore>();
        builder.Services.AddScoped<IDeskSsoAuthority, DeskSsoAuthority>();
        builder.Services.AddScoped(provider =>
        {
            var options = provider.GetRequiredService<IOptions<DeskSsoOptions>>().Value;
            var clients = options.Enabled ? options.Clients : [];
            return new DeskSsoService(options.Enabled, clients.ToDictionary(client => client.Id, StringComparer.Ordinal),
                provider.GetRequiredService<ISsoGrantStore>(), provider.GetRequiredService<IDeskSsoAuthority>(), provider.GetRequiredService<TimeProvider>());
        });
        return builder;
    }

    public static WebApplication MapDeskSsoEndpoints(this WebApplication app)
    {
        app.UseMiddleware<DeskSsoRequestBoundary>();
        app.MapGet("/api/sso/authorize", AuthorizeAsync).AllowAnonymous().RequireRateLimiting(DeskSsoRatePolicy.Name);
        app.MapPost("/api/sso/exchange", ExchangeAsync).AllowAnonymous().WithMetadata(new SkipAntiforgeryValidation()).RequireRateLimiting(DeskSsoRatePolicy.Name);
        app.MapPost("/api/sso/refresh", RefreshAsync).AllowAnonymous().WithMetadata(new SkipAntiforgeryValidation()).RequireRateLimiting(DeskSsoRatePolicy.Name);
        app.MapPost("/api/sso/revoke", RevokeAsync).AllowAnonymous().WithMetadata(new SkipAntiforgeryValidation()).RequireRateLimiting(DeskSsoRatePolicy.Name);
        return app;
    }

    private static async Task<IResult> AuthorizeAsync(string? clientId, string? redirectUri, string? codeChallenge,
        string? state, HttpContext http, DeskSsoService service)
    {
        NoStore(http);
        var client = service.FindClient(clientId);
        if (!http.Request.IsHttps || client is null || redirectUri != client.RedirectUri || !DeskSsoService.IsChallenge(codeChallenge)
            || state is not { Length: >= 32 and <= 256 } || !state.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))
            return Problem(400, "Invalid delegation request");
        var authentication = await http.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        if (!authentication.Succeeded)
            return Results.LocalRedirect(QueryHelpers.AddQueryString("/", "returnUrl", http.Request.Path + http.Request.QueryString));
        var session = AccessClaims.Session(http.User);
        var expires = authentication.Properties?.ExpiresUtc;
        if (session is null || expires is null) return Problem(401, "Authentication required");
        var code = await service.AuthorizeAsync(clientId!, redirectUri!, codeChallenge!,
            new(AccessClaims.UserId(http.User), session.Value, client.OrganizationId, expires.Value), http.RequestAborted);
        return code is null ? Problem(403, "This account cannot access the application")
            : Results.Redirect(QueryHelpers.AddQueryString(client.RedirectUri, new Dictionary<string, string?> { ["code"] = code, ["state"] = state }));
    }

    private static async Task<IResult> ExchangeAsync(SsoExchangeInput input, HttpContext http, DeskSsoService service)
    {
        NoStore(http);
        if (!IsBackchannel(http)) return Problem(400, "Invalid delegation request");
        var response = await service.ExchangeAsync(input.ClientId, input.ClientSecret, input.RedirectUri, input.Code, input.CodeVerifier, http.RequestAborted);
        return response is null ? Problem(400, "Invalid delegation grant") : Results.Ok(new { data = response });
    }

    private static async Task<IResult> RefreshAsync(SsoRefreshInput input, HttpContext http, DeskSsoService service)
    {
        NoStore(http);
        if (!IsBackchannel(http)) return Problem(400, "Invalid delegation request");
        var response = await service.RefreshAsync(input.ClientId, input.ClientSecret, input.RefreshToken, http.RequestAborted);
        return response is null ? Problem(400, "Invalid delegation grant") : Results.Ok(new { data = response });
    }

    private static async Task<IResult> RevokeAsync(SsoRefreshInput input, HttpContext http, DeskSsoService service)
    {
        NoStore(http);
        if (!IsBackchannel(http)) return Problem(400, "Invalid delegation request");
        return await service.RevokeAsync(input.ClientId, input.ClientSecret, input.RefreshToken, http.RequestAborted)
            ? Results.NoContent() : Problem(400, "Invalid delegation grant");
    }

    private static bool IsBackchannel(HttpContext http)
    {
        return http.Request.IsHttps && !http.Request.Headers.ContainsKey("Origin") && !http.Request.Headers.ContainsKey("Sec-Fetch-Site");
    }
    private static IResult Problem(int status, string title)
    {
        return Results.Problem(statusCode: status, title: title);
    }
    private static void NoStore(HttpContext http)
    {
        http.Response.Headers.CacheControl = "no-store";
        http.Response.Headers["Referrer-Policy"] = "no-referrer";
    }
}

public sealed record SsoExchangeInput(string ClientId, string ClientSecret, string RedirectUri, string Code, string CodeVerifier);
public sealed record SsoRefreshInput(string ClientId, string ClientSecret, string RefreshToken);
