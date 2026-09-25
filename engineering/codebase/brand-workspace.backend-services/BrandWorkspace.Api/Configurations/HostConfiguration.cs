namespace BrandWorkspace.Api.Configurations;

/// <summary>Extends the host builder and the web application for startup wiring.</summary>
public static partial class HostConfiguration
{
    /// <summary>Configures the application builder (services).</summary>
    public static WebApplicationBuilder Configure(this WebApplicationBuilder builder, bool migrate = false) =>
        builder.AddExplicitEnvironment().AddPostgresDatabase().AddFoundation(migrate).AddHttpSurface();

    /// <summary>Runs startup tasks, then configures middleware and endpoints.</summary>
    public static WebApplication Configure(this WebApplication app) => app.AddFoundationEndpoints();
}
