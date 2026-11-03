using WoW.Two.Sdk.Beta.Be.Foundation.Configuration;
using WoW.Two.Sdk.Beta.Be.Web.Hosting;
using WoW.Two.Sdk.Beta.Be.Web.ProblemDetails;
using OcharoCatalogue.Api.Catalogue;

namespace OcharoCatalogue.Api.Configurations;

/// <summary>Extends the host builder and the web application for startup wiring.</summary>
public static partial class HostConfiguration
{
    /// <summary>Configures the application builder (services).</summary>
    public static WebApplicationBuilder Configure(this WebApplicationBuilder builder)
    {
        builder.AddSettingsFiles();
        builder
            .AddSqliteDatabase()
            .AddClock()
            .AddCatalogue()
            .AddAccess()
            .AddMediator()
            .AddRuntimeConfig()
            .AddControllers();

        return builder;
    }

    /// <summary>Runs startup tasks, then configures middleware and endpoints.</summary>
    public static async Task<WebApplication> ConfigureAsync(this WebApplication app)
    {
        await app.InitializeCatalogueAsync();

        app.UseProxyAwareHosting();
        app.UseHttpProblemDetails();

        app.UseMiddleware<CatalogueExceptionMiddleware>();
        app.UseMiddleware<CatalogueHttpBoundaryMiddleware>();

        // Serve the Vue SPA from wwwroot (single-deploy: API + UI in one host), ahead of sign-in: the shell is public.
        app.UseDefaultFiles();
        app.UseStaticFiles();

        // Every endpoint below needs a member of the bound organization unless it allows anonymous callers: the published
        // reader and its API, the public feed, public media, runtime config, status, health and the SPA shell.
        app.UseRouting();
        app.UseAccess();

        if (app.Environment.IsLocalRun(app.Configuration))
            app.MapOpenApi().AllowAnonymous();

        app.WarnAboutRejectedRuntimeConfig();
        app.MapControllers();
        // Retained for existing callers; `GET api/system/status` is the conventional status endpoint.
        app.MapGet("/api/system/version", (HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            return Results.Ok(new { service = "OcharoCatalogue", version = BuildIdentity.Version });
        }).AllowAnonymous();
        app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "OcharoCatalogue" })).AllowAnonymous();
        app.MapFallback("/api/{**slug}", () => Results.Problem(statusCode: 404, title: "API route not found")).AllowAnonymous();
        app.MapFallbackToFile("index.html").AllowAnonymous();

        return app;
    }
}
