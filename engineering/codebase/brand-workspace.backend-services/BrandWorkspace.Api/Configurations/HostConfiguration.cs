using BrandWorkspace.Api.MediaProcessing;
using BrandWorkspace.Api.Commerce;
using BrandWorkspace.Api.Integrations;
using BrandWorkspace.Api.DesignDocuments;
using BrandWorkspace.Api.Access;
using BrandWorkspace.Api.Preparation;

namespace BrandWorkspace.Api.Configurations;

/// <summary>Extends the host builder and the web application for startup wiring.</summary>
public static partial class HostConfiguration
{
    /// <summary>Configures the application builder (services).</summary>
    public static WebApplicationBuilder Configure(this WebApplicationBuilder builder, bool migrate = false) =>
        builder.AddExplicitEnvironment().AddPostgresDatabase().AddFoundation(migrate).AddAccess().AddPreparation().AddHttpSurface();

    /// <summary>Runs startup tasks, then configures middleware and endpoints.</summary>
    public static WebApplication Configure(this WebApplication app, bool serveHttp = true) =>
        serveHttp ? app.AddFoundationEndpoints().MapAccessEndpoints().MapPreparationEndpoints().MapDesignDocumentEndpoints().MapIntegrationEndpoints().MapCommerceEndpoints().MapMediaProcessingEndpoints() : app;
}
