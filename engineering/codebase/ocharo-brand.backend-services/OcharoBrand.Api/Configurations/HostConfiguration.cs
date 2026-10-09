using Ocharo.Sso;
using WoW.Two.Sdk.Backend.Beta.Web.ProblemDetails;
using OcharoBrand.Api.Brand;

namespace OcharoBrand.Api.Configurations;

/// <summary>Extends the host builder and the web application for startup wiring.</summary>
public static partial class HostConfiguration
{
    /// <summary>Configures the application builder (services).</summary>
    public static WebApplicationBuilder Configure(this WebApplicationBuilder builder)
    {
        builder.Services.AddOcharoSso(builder.Configuration, "brand", "ocharo-brand", requireBoundOrganization: true);
        return builder.AddSqliteDatabase().AddBrand().AddHttpSurface();
    }

    /// <summary>Runs startup tasks, then configures middleware and endpoints.</summary>
    public static Task<WebApplication> ConfigureAsync(this WebApplication app) =>
        app.ConfigureHttpSurface().InitializeBrandAsync();
}
