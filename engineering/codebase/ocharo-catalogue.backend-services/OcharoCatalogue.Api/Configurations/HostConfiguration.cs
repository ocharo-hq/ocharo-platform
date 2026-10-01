using OcharoCatalogue.Api.Catalogue;

namespace OcharoCatalogue.Api.Configurations;

/// <summary>Configures the OcharoCatalogue host — slim orchestrator that delegates to extension methods.</summary>
public static class HostConfiguration
{
    /// <summary>Configures all services across every layer.</summary>
    public static WebApplicationBuilder Configure(this WebApplicationBuilder builder)
    {
        builder
            .AddPersistenceLayer()
            .AddInfrastructureLayer()
            .AddApplicationLayer()
            .AddApiServices();

        return builder;
    }

    /// <summary>Configures the middleware pipeline and maps endpoints.</summary>
    public static WebApplication Configure(this WebApplication app)
    {
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
