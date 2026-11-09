using BrandWorkspace.Api.MediaProcessing;
using BrandWorkspace.Api.Commerce;
using BrandWorkspace.Api.DesignDocuments;
using BrandWorkspace.Api.Access;
using BrandWorkspace.Api.Preparation;
using BrandWorkspace.Api.Sourcing;
using BrandWorkspace.Api.Events;

namespace BrandWorkspace.Api.Configurations;

/// <summary>Extends the host builder and the web application for startup wiring.</summary>
public static partial class HostConfiguration
{
    /// <summary>Configures the application builder (services).</summary>
    public static WebApplicationBuilder Configure(this WebApplicationBuilder builder, bool migrate = false) =>
        builder.AddExplicitEnvironment().AddPostgresDatabase().AddFoundation(migrate).AddAccess()
            .AddDeskFiles().AddPreparation().AddDesignDocuments().AddCommerce().AddSourcing().AddEvents().AddMediaProcessing().AddRuntimeConfig().AddHttpSurface();

    /// <summary>Runs startup tasks, then configures middleware and endpoints.</summary>
    public static WebApplication Configure(this WebApplication app, bool serveHttp = true) =>
        serveHttp ? app.AddFoundationEndpoints().MapAccessEndpoints().MapPreparationEndpoints().MapDesignDocumentEndpoints().MapCommerceEndpoints().MapSourcingEndpoints().MapEventEndpoints().MapMediaProcessingEndpoints().MapRuntimeConfig() : app;
}
