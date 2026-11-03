using WoW.Two.Sdk.Beta.Be.Foundation.Configuration;

namespace OcharoBrand.Api.Configurations;

/// <summary>Extends the host builder and the web application for startup wiring.</summary>
public static partial class HostConfiguration
{
    /// <summary>Configures the application builder (services).</summary>
    public static WebApplicationBuilder Configure(this WebApplicationBuilder builder)
    {
        builder.AddSettingsFiles();
        return builder.AddAccess().AddSqliteDatabase().AddBrand().AddRuntimeConfig().AddHttpSurface();
    }

    /// <summary>Runs startup tasks, then configures middleware and endpoints.</summary>
    public static Task<WebApplication> ConfigureAsync(this WebApplication app) =>
        app.ConfigureHttpSurface().InitializeBrandAsync();
}
