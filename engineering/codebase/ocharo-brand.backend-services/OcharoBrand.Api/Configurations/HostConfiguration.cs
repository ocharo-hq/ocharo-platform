using WoW.Two.Sdk.Beta.Be.Meta;

namespace OcharoBrand.Api.Configurations;

/// <summary>Extends the host builder and the web application for startup wiring.</summary>
public static partial class HostConfiguration
{
    /// <summary>Configures the application builder (services).</summary>
    public static WebApplicationBuilder Configure(this WebApplicationBuilder builder)
    {
        // Lay the SDK boot floor first. It reads the settings file a target mounts (appsettings.Target.json) and, on a
        // local run, this machine's (appsettings.Local.json). The operator SPA is served from the same host.
        builder.AddApiDefaults(options =>
        {
            options.ServiceName = "ocharo-brand";
            options.EnableOtlpExporters = false;
            options.EnableRateLimiting = false;
            options.EnableOutputCache = false;
            // Other Ocharo origins embed the public kit's logos and media, so no cross-origin embedder policy.
            options.EnableCrossOriginEmbedderPolicy = false;
            // The ingress terminates TLS; Brand never redirected.
            options.EnableHttpsRedirection = false;
            options.SpaHosting = _ => { };
            // Behind the TLS-terminating ingress, same-origin write checks need the forwarded scheme; any other sender
            // keeps its own.
            options.TrustProxiesFrom(builder.Configuration.GetSection("Deployment"));
        });

        return builder.AddAccess().AddSqliteDatabase().AddBrand().AddRuntimeConfig().AddHttpSurface();
    }

    /// <summary>Runs startup tasks, then configures middleware and endpoints.</summary>
    public static async Task<WebApplication> ConfigureAsync(this WebApplication app)
    {
        await app.InitializeBrandAsync();
        return app.ConfigureHttpSurface();
    }
}
