using WoW.Two.Sdk.Beta.Be.Foundation.Configuration;
using WoW.Two.Sdk.Beta.Be.Meta;

namespace OcharoCatalogue.Api.Configurations;

/// <summary>Extends the host builder and the web application for startup wiring.</summary>
public static partial class HostConfiguration
{
    /// <summary>Configures the application builder (services).</summary>
    public static WebApplicationBuilder Configure(this WebApplicationBuilder builder)
    {
        // Lay the SDK boot floor first; it reads the target's and a local run's settings files. Catalogue never redirected
        // to HTTPS (the ingress terminates TLS), never rate-limited or cached output, and other Ocharo origins embed its
        // public media and release files, so those stay as they were. OpenAPI is mapped below, anonymous on a local run.
        builder.AddApiDefaults(options =>
        {
            options.ServiceName = "ocharo-catalogue";
            options.EnableOtlpExporters = false;
            options.EnableRateLimiting = false;
            options.EnableOutputCache = false;
            options.EnableCrossOriginEmbedderPolicy = false;
            options.EnableHttpsRedirection = false;
            options.ExposeOpenApi = false;
            options.SpaHosting = _ => { };
            options.TrustProxiesFrom(builder.Configuration.GetSection("Deployment"));
        });

        builder
            .AddSqliteDatabase()
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

        // SDK pipeline: secure headers, compression, the SPA bundle (public, ahead of sign-in), /health (database
        // readiness) and the SPA fallback with its JSON 404 under /api. Every endpoint needs a member of the bound
        // organization unless it allows anonymous callers: the published reader and its API, the public feed, public
        // media, runtime config, status, health and the SPA shell.
        app.UseApiDefaults(pipeline => pipeline
            .UseCatalogueBoundary()
            .UseAccess());

        if (app.Environment.IsLocalRun(app.Configuration))
            app.MapOpenApi().AllowAnonymous();

        app.WarnAboutRejectedRuntimeConfig();
        app.MapAccess();
        app.MapControllers();

        return app;
    }
}
