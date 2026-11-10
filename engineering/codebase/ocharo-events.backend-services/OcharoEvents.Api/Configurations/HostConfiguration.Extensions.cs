using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using OcharoEvents.Api.RuntimeConfig.Settings;
using OcharoEvents.Application.Events.Repositories;
using OcharoEvents.Infrastructure;
using OcharoEvents.Infrastructure.Events.Services;
using OcharoEvents.Persistence;
using WoW.Two.Sdk.Beta.Be.Data.Abstractions;
using WoW.Two.Sdk.Beta.Be.Data.EntityFrameworkCore;
using WoW.Two.Sdk.Beta.Be.Data.Migrations.Bespoke;
using WoW.Two.Sdk.Beta.Be.Events.Client;
using WoW.Two.Sdk.Beta.Be.Foundation.Configuration;
using WoW.Two.Sdk.Beta.Be.Foundation.Options;
using WoW.Two.Sdk.Beta.Be.Identity.Bff;
using WoW.Two.Sdk.Beta.Be.Mediator;
using WoW.Two.Sdk.Beta.Be.Observability.HealthChecks;
using WoW.Two.Sdk.Beta.Be.Web.Json;
using WoW.Two.Sdk.Beta.Be.Web.ProblemDetails;
using WoW.Two.Sdk.Beta.Be.Meta;

namespace OcharoEvents.Api.Configurations;

public static partial class HostConfiguration
{
    /// <summary>Holds the root URL of the WoW2 Events instance that serves the Events page.</summary>
    private const string EventsBaseUrlKey = "Events:BaseUrl";

    /// <summary>Registers the service's place in the product database (<c>ConnectionStrings:Database</c>, filled per target
    /// by Wheelhouse): the <c>ocharo_events</c> migration set in schema <c>ocharo_events</c>, which holds only the Data
    /// Protection key ring, the context the readiness check on the bundle's <c>/health</c> opens.</summary>
    /// <remarks>The service stores no event and no organization row, so its set keys into no Identity table and depends on
    /// none.</remarks>
    private static WebApplicationBuilder AddPostgresDatabase(this WebApplicationBuilder builder)
    {
        // The configured string, not the data source's: a data source drops the password from its own copy.
        static string ConnectionString(IServiceProvider provider) =>
            provider.GetRequiredService<IConfiguration>().GetConnectionString(EventsDatabaseConstants.ConnectionStringName)
            ?? throw new InvalidOperationException($"ConnectionStrings:{EventsDatabaseConstants.ConnectionStringName} is required.");
        builder.Services.AddSingleton(provider => NpgsqlDataSource.Create(ConnectionString(provider)));
        builder.Services.AddKeyedSingleton<IDbConnectionFactory>(EventsDatabaseConstants.MigrationSetName, static (provider, _) =>
            new DataSourceConnectionFactory(provider.GetRequiredService<NpgsqlDataSource>()));
        builder.Services.AddKeyedSingleton(EventsDatabaseConstants.MigrationSetName, static (provider, _) =>
            new DatabaseSettings { ConnectionString = ConnectionString(provider) });
        builder.Services.AddDatabaseBespokeMigrationSet(
            EventsDatabaseConstants.MigrationSetName,
            static _ => new EmbeddedResourceMigrationBroker(typeof(OcharoEventsDbContext).Assembly, EventsDatabaseConstants.MigrationResourcePrefix),
            static migration =>
            {
                migration.SchemaName = EventsDatabaseConstants.Schema;
                migration.SchemaOwner = "product";
                migration.AdvisoryLockId = EventsDatabaseConstants.AdvisoryLockId;
                migration.Version = BuildIdentity.MigrationVersion;
                migration.Release = BuildIdentity.MigrationRelease;
            });
        builder.Services.AddDbContext<OcharoEventsDbContext>((provider, options) =>
            options.UseNpgsql(provider.GetRequiredService<NpgsqlDataSource>()).UseSnakeCaseNamingConvention());
        builder.Services.AddHealthChecks().AddDatabaseReadinessCheck<OcharoEventsDbContext>();
        return builder;
    }

    /// <summary>
    /// Registers the showcase events feed on WoW2 Events through its published client, whose person flow exchanges the
    /// signed-in person's BFF session token for one with Events' audience by this host's own Identity client
    /// (<c>Identity:Client</c>, client 11 <c>ocharo-events</c>). The service keeps no events store: a deployed run refuses
    /// to start without <see cref="EventsBaseUrlKey"/>, a local run without it answers 503 on the Events routes. Settings
    /// read at resolve time, so a test host's settings decide.
    /// </summary>
    private static WebApplicationBuilder AddEvents(this WebApplicationBuilder builder)
    {
        builder.Services.AddOptions<EventsServiceOptions>()
            .BindConfiguration(EventsServiceOptions.SectionName)
            .Validate<IConfiguration, IHostEnvironment>(
                (events, configuration, environment) =>
                    !string.IsNullOrWhiteSpace(events.BaseUrl) || environment.IsLocalRun(configuration),
                $"{EventsBaseUrlKey} is required: WoW2 Events serves the Events page, and this service keeps no events store.")
            .ValidateOnStart();
        builder.Services.AddEventsClient(_ => { });
        builder.Services.AddOptions<EventsClientOptions>().Configure<IConfiguration>((events, configuration) =>
        {
            if (configuration[EventsBaseUrlKey] is not { Length: > 0 } url) return;
            events.BaseAddress = new Uri(url);
            if (Uri.TryCreate(configuration["Identity:Client:Authority"], UriKind.Absolute, out var authority))
                events.Identity = new EventsClientIdentityOptions
                {
                    TokenEndpoint = new Uri(authority, "connect/token"),
                    ClientId = configuration["Identity:Client:ClientId"] ?? string.Empty,
                    ClientSecret = configuration["Identity:Client:ClientSecret"] ?? string.Empty,
                };
        });
        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddScoped<IEventRepository, EventsServiceRepository>();
        builder.Services.AddSingleton<EventValidationService>();
        builder.Services.AddScoped<EventService>();
        // The SDK registration converts a handler's thrown failure into its AppResult, outermost.
        builder.Services.AddMediator(typeof(InfrastructureAssembly).Assembly);
        return builder;
    }

    /// <summary>Registers the public runtime settings the SPA reads at startup: the Ocharo app and site addresses.</summary>
    private static WebApplicationBuilder AddRuntimeConfig(this WebApplicationBuilder builder)
    {
        // Every address is optional, and a malformed one is left out rather than failing the host over a menu link.
        builder.Services.AddValidatedSettings<OcharoAppsSettings>(builder.Configuration, OcharoAppsSettings.SectionName, _ => { });
        return builder;
    }

    /// <summary>Warns once about each configured app address the runtime config leaves out.</summary>
    private static void WarnAboutRejectedRuntimeConfig(this WebApplication app)
    {
        var rejected = app.Services.GetRequiredService<OcharoAppsSettings>().RejectedSettings();
        if (rejected.Count > 0)
            app.Logger.LogWarning("Runtime config leaves out {Settings}: each must be a plain HTTPS address.", string.Join(", ", rejected));
    }

    /// <summary>Registers controller delivery on the SDK wire contract and SDK problem details for the HTTP surface, whatever
    /// the client accepts; the boot floor holds forwarded headers (loopback and <c>Deployment:TrustedProxies</c>) and
    /// OpenAPI.</summary>
    private static WebApplicationBuilder AddHttpSurface(this WebApplicationBuilder builder)
    {
        builder.Services.AddHttpProblemDetails();
        builder.Services.AddControllersWithSdkJson();
        return builder;
    }

    /// <summary>Configures the SDK pipeline — secure headers, compression, the Events SPA, <c>/health</c> and the SPA
    /// fallback, whose unknown <c>/api</c> routes answer a JSON 404 — with SDK problem details and the session pipeline
    /// between routing and the endpoints, then the Events API and the Identity session routes. Static files, the SPA
    /// fallback, <c>/api/runtime-config</c> and <c>/api/system/status</c> answer anonymously; the organization routes need a
    /// session and pass the organization access filter.</summary>
    private static WebApplication ConfigureHttpSurface(this WebApplication app)
    {
        app.UseApiDefaults(pipeline =>
        {
            pipeline.UseHttpProblemDetails();
            pipeline.UseAccess();
        });
        app.WarnAboutRejectedRuntimeConfig();
        app.MapControllers();
        app.MapIdentityBffEndpoints();
        return app;
    }
}
