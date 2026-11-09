using BrandWorkspace.Api.MediaProcessing;
using BrandWorkspace.Api.Commerce;
using BrandWorkspace.Api.DesignDocuments;
using BrandWorkspace.Api.Access;
using BrandWorkspace.Api.Preparation;
using BrandWorkspace.Api.Sourcing;
using BrandWorkspace.Api.Events;
using BrandWorkspace.Api.Foundation;
using BrandWorkspace.Infrastructure.Preparation.Services;
using WoW.Two.Sdk.Beta.Be.Meta;

namespace BrandWorkspace.Api.Configurations;

/// <summary>Extends the host builder and the web application for startup wiring.</summary>
public static partial class HostConfiguration
{
    /// <summary>Configures the application builder (services).</summary>
    /// <param name="builder">The web application builder to configure.</param>
    public static WebApplicationBuilder Configure(this WebApplicationBuilder builder)
    {
        // Lay the SDK boot floor before any domain seam. It reads the settings files (appsettings.Target.json, and
        // appsettings.Local.json on a local run whose launch profile asks); Desk then keeps only its whitelisted environment
        // inputs, so the bundle reads the ingress it trusts as Desk reads every setting. The SPA is served from the same host.
        builder.AddApiDefaults(options => builder.AddExplicitEnvironment().ConfigureApiDefaults(options));

        return builder.AddPostgresDatabase().AddFoundation().AddAccess()
            .AddDeskFiles().AddPreparation().AddDesignDocuments().AddCommerce().AddSourcing().AddEvents().AddMediaProcessing().AddRuntimeConfig().AddHttpSurface();
    }

    /// <summary>Runs startup tasks, then configures middleware and endpoints.</summary>
    /// <param name="app">The built web application to configure.</param>
    public static async Task<WebApplication> ConfigureAsync(this WebApplication app)
    {
        // Apply the schema before the API listens, deployed included (Desk's deploy has no migrate step yet), so /health never
        // reports ready ahead of it; then fill the Files ids of rows the migration moved.
        await app.MigrateOnStartupAsync();
        await app.BackfillFileIdsAsync();
        return app.Configure();
    }

    /// <summary>Configures middleware and endpoints, without startup tasks.</summary>
    /// <param name="app">The built web application to configure.</param>
    public static WebApplication Configure(this WebApplication app) =>
        app.UseHttpSurface().MapAccessEndpoints().MapPreparationEndpoints().MapDesignDocumentEndpoints().MapCommerceEndpoints().MapSourcingEndpoints()
            .MapEventEndpoints().MapMediaProcessingEndpoints().MapControllerEndpoints();

    /// <summary>
    /// Adds Desk's own verbs beside the be sdk's <c>migrate</c> and <c>config check</c>: <c>import sourcing</c> and
    /// <c>import events</c>, which import a bundle as an existing editor, and <c>media verify</c>, the image build's decoder
    /// probe. Each runs on the built host without serving.
    /// </summary>
    /// <param name="verbs">The service host's verbs.</param>
    public static ServiceVerbs AddDeskVerbs(this ServiceVerbs verbs)
    {
        ArgumentNullException.ThrowIfNull(verbs);
        return verbs
            .AddHostVerb(ImportVerb, (services, verb, ct) => verb.Arguments switch
            {
                [SourcingImportCommand.Name, .. var arguments] => SourcingImportCommand.RunAsync(services, arguments, verb.Output, verb.Error, ct),
                [EventImportCommand.Name, .. var arguments] => EventImportCommand.RunAsync(services, arguments, verb.Output, verb.Error, ct),
                _ => throw new ArgumentException(ImportUsage),
            })
            .AddHostVerb(MediaVerb, (services, verb, _) => verb.Arguments is [MediaVerifyArgument]
                ? Task.FromResult(MediaRuntimeVerification.Run(services.GetRequiredService<ImageValidationService>(), verb.Output))
                : throw new ArgumentException(MediaUsage));
    }
}
