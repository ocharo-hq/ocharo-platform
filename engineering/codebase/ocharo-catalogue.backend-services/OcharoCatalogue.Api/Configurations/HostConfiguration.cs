using OcharoCatalogue.Api.Catalogue;

namespace OcharoCatalogue.Api.Configurations;

/// <summary>Extends the host builder and the web application for startup wiring.</summary>
public static partial class HostConfiguration
{
    /// <summary>Configures the application builder (services).</summary>
    public static WebApplicationBuilder Configure(this WebApplicationBuilder builder)
    {
        builder
            .AddSqliteDatabase()
            .AddClock()
            .AddCatalogue()
            .AddMediator()
            .AddControllers();

        return builder;
    }

    /// <summary>Runs startup tasks, then configures middleware and endpoints.</summary>
    public static async Task<WebApplication> ConfigureAsync(this WebApplication app)
    {
        await app.InitializeCatalogueAsync();


        app.UseMiddleware<CatalogueExceptionMiddleware>();
        app.UseMiddleware<LocalPilotBoundaryMiddleware>();

        if (app.Environment.IsDevelopment())
            app.MapOpenApi();

        // Serve the Vue SPA from wwwroot (single-deploy: API + UI in one host).
        app.UseDefaultFiles();
        app.UseStaticFiles();

        app.MapControllers();
        app.MapGet("/api/system/version", (HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            return Results.Ok(new { service = "OcharoCatalogue", version = BuildIdentity.Version });
        }).AllowAnonymous();
        app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "OcharoCatalogue", mode = "local-pilot" }));
        app.MapFallback("/api/{**slug}", () => Results.Problem(statusCode: 404, title: "API route not found"));
        app.MapFallbackToFile("index.html");

        return app;
    }
}
