using WoW.Two.Sdk.Beta.Be.Data;
using WoW.Two.Sdk.Beta.Be.Meta;

namespace OcharoEvents.Api.Configurations;

/// <summary>Extends the host builder and the web application for startup wiring.</summary>
public static partial class HostConfiguration
{
    /// <summary>Configures the application builder (services).</summary>
    public static WebApplicationBuilder Configure(this WebApplicationBuilder builder)
    {
        // Lay the SDK boot floor first. It reads the settings file a target mounts (appsettings.Target.json) and, on a
        // local run, this machine's (appsettings.Local.json). The Events SPA is served from the same host.
        builder.AddApiDefaults(options =>
        {
            options.ServiceName = "ocharo-events";
            options.EnableOtlpExporters = false;
            options.EnableRateLimiting = false;
            options.EnableOutputCache = false;
            options.EnableCrossOriginEmbedderPolicy = false;
            // The ingress terminates TLS.
            options.EnableHttpsRedirection = false;
            options.SpaHosting = _ => { };
            // Behind the TLS-terminating ingress, sign-in and same-origin write checks need the forwarded scheme; any
            // other sender keeps its own.
            options.TrustProxiesFrom(builder.Configuration.GetSection("Deployment"));
        });

        return builder.AddPostgresDatabase().AddAccess().AddEvents().AddRuntimeConfig().AddHttpSurface();
    }

    /// <summary>Runs startup tasks, then configures middleware and endpoints.</summary>
    public static async Task<WebApplication> ConfigureAsync(this WebApplication app)
    {
        // A local run applies every set at boot; a deployed boot only verifies the levels the migrate step applied.
        await app.Services.MigrateBespokeOnStartupAsync(app.Lifetime.ApplicationStopping);
        return app.ConfigureHttpSurface();
    }
}
