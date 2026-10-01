using OcharoBrand.Api.Brand;

namespace OcharoBrand.Api.Configurations;

/// <summary>Configures the OcharoBrand host — admin API, public brand kit and the operator SPA.</summary>
public static class HostConfiguration
{
    public static WebApplicationBuilder Configure(this WebApplicationBuilder builder)
    {
        builder.AddPersistenceLayer().AddInfrastructureLayer().AddApplicationLayer().AddApiServices();
        return builder;
    }

    public static WebApplication Configure(this WebApplication app)
    {
        app.UseMiddleware<BrandExceptionMiddleware>();
        app.UseMiddleware<LocalPilotBoundaryMiddleware>();
        if (app.Environment.IsDevelopment())
            app.MapOpenApi();
        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.MapControllers();
        app.MapGet("/api/system/version", (HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            return Results.Ok(new { service = "OcharoBrand", version = BuildIdentity.Version });
        }).AllowAnonymous();
        app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "OcharoBrand", mode = "local-pilot" }));
        app.MapFallback("/api/{**slug}", () => Results.Problem(statusCode: 404, title: "API route not found"));
        app.MapFallbackToFile("index.html");
        return app;
    }
}
