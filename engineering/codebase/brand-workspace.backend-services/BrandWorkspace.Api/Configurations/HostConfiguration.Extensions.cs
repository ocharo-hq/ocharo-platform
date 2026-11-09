using WoW.Two.Sdk.Beta.Be.Foundation.Configuration;
using WoW.Two.Sdk.Beta.Be.Foundation.Options;
using WoW.Two.Sdk.Beta.Be.Web.ProblemDetails;
using BrandWorkspace.Api.Access;
using BrandWorkspace.Api.MediaProcessing.Constants;
using BrandWorkspace.Application.Commerce.Services;
using BrandWorkspace.Infrastructure;
using WoW.Two.Sdk.Beta.Be.Mediator;
using WoW.Two.Sdk.Beta.Be.Mediator.Logging;
using WoW.Two.Sdk.Beta.Be.Web.Json;
using BrandWorkspace.Api.Foundation;
using BrandWorkspace.Infrastructure.Preparation.Services;
using BrandWorkspace.Application.Preparation.Repositories;
using BrandWorkspace.Application.Preparation.Services;
using BrandWorkspace.Persistence.Preparation.Repositories;
using BrandWorkspace.Application.DesignDocuments.Repositories;
using BrandWorkspace.Infrastructure.DesignDocuments.Services;
using BrandWorkspace.Persistence.DesignDocuments.Repositories;
using BrandWorkspace.Application.Commerce.Models;
using BrandWorkspace.Application.Commerce.Repositories;
using BrandWorkspace.Infrastructure.Commerce.Services;
using BrandWorkspace.Persistence.Commerce.Repositories;
using BrandWorkspace.Persistence.Commerce.Serializers;
using BrandWorkspace.Application.Sourcing.Repositories;
using BrandWorkspace.Infrastructure.Sourcing.Services;
using BrandWorkspace.Persistence.Sourcing.Repositories;
using BrandWorkspace.Api.RuntimeConfig.Settings;
using BrandWorkspace.Application.Events.Repositories;
using BrandWorkspace.Infrastructure.Events.Services;
using BrandWorkspace.Persistence.Events.Repositories;
using WoW.Two.Sdk.Beta.Be.Media.Images;
using WoW.Two.Sdk.Beta.Be.Media.BackgroundRemoval;
using BrandWorkspace.Application.Access.Models;
using BrandWorkspace.Application.Access.Repositories;
using BrandWorkspace.Persistence.Access.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using WoW.Two.Sdk.Beta.Be.Identity.Authorization;
using WoW.Two.Sdk.Beta.Be.Identity.Bff;
using WoW.Two.Sdk.Beta.Be.Identity.Jwt;
using WoW.Two.Sdk.Beta.Be.Identity.Signals;
using WoW.Two.Sdk.Beta.Be.Caching.Postgres;
using System.Data.Common;
using BrandWorkspace.Application.Foundation.Repositories;
using BrandWorkspace.Domain.Foundation.Models;
using BrandWorkspace.Persistence.Foundation.Repositories;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Npgsql;
using WoW.Two.Sdk.Beta.Be.Data.Abstractions;
using WoW.Two.Sdk.Beta.Be.Data.Dapper;
using WoW.Two.Sdk.Beta.Be.Data.Migrations.Bespoke;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;
using WoW.Two.Sdk.Beta.Be.Files.Client;
using BrandWorkspace.Application.Files.Constants;
using BrandWorkspace.Application.Files.Models;
using BrandWorkspace.Application.Files.Services;
using BrandWorkspace.Application.Files.Repositories;
using BrandWorkspace.Infrastructure.Files.Services;
using BrandWorkspace.Persistence.Files.Repositories;
using WoW.Two.Sdk.Beta.Be.Meta;
using WoW.Two.Sdk.Beta.Be.Web.DataProtection;

namespace BrandWorkspace.Api.Configurations;

public static partial class HostConfiguration
{
    /// <summary>Holds the verb that imports a bundle: <c>import sourcing</c> or <c>import events</c>.</summary>
    private const string ImportVerb = "import";

    /// <summary>Holds the verb of the media runtime probe the image build runs: <c>media verify</c>.</summary>
    private const string MediaVerb = "media";

    /// <summary>Holds the one argument <see cref="MediaVerb"/> takes.</summary>
    private const string MediaVerifyArgument = "verify";

    private const string ImportUsage =
        "Use import sourcing|events --bundle <bundle.json> --organization <organization-id> "
        + "--actor <identity-user-id|email> [--dry-run].";

    private const string MediaUsage = "Use media verify alone.";

    /// <summary>
    /// Keeps only the permitted deployment environment inputs, after the settings files the boot floor added: every
    /// ambient environment source leaves, and the whitelisted variables return last.
    /// </summary>
    private static WebApplicationBuilder AddExplicitEnvironment(this WebApplicationBuilder builder)
    {
        EnvironmentVariablesConfigurationSource[] environmentSources = builder.Configuration.Sources
            .OfType<EnvironmentVariablesConfigurationSource>()
            .ToArray();
        foreach (var source in environmentSources)
            builder.Configuration.Sources.Remove(source);
        var values = new Dictionary<string, string?>();
        if (Environment.GetEnvironmentVariable("ConnectionStrings__Database") is { } connection)
            values["ConnectionStrings:Database"] = connection;
        if (Environment.GetEnvironmentVariable("ASPNETCORE_URLS") is { } urls) values["urls"] = urls;
        if (Environment.GetEnvironmentVariable("AllowedHosts") is { } allowedHosts)
            values["AllowedHosts"] = allowedHosts;
        string[] permitted =
        [
            "ImageProcessing__WorkerBaseUrl", "ImageProcessing__WorkerKey", "Commerce__SourceRoot",
            "Commerce__AdoptionOrganizationId", "DataProtection__KeysPath", "DataProtection__ApplicationName",
            "Files__BaseUrl", "Kestrel__Certificates__Default__Path", "Kestrel__Certificates__Default__Password",
            "Identity__Client__Authority", "Identity__Client__ClientId", "Identity__Client__ClientSecret",
            "Identity__Client__CookieName", "Identity__Client__PostSignOutRedirectPath",
            "Identity__Client__Environment", "Identity__Resource__Uri",
        ];
        foreach (var key in permitted)
            if (Environment.GetEnvironmentVariable(key) is { } value) values[key.Replace("__", ":")] = value;
        builder.Configuration.AddInMemoryCollection(values);
        return builder;
    }

    /// <summary>
    /// Tunes the SDK boot floor to Desk's existing behaviour: no OTLP export, rate limit, output cache, embedder policy
    /// or HTTPS redirect (the ingress terminates TLS); the SPA served from the host; forwarded headers trusted only
    /// from loopback and <c>Deployment:TrustedProxies</c>.
    /// </summary>
    private static void ConfigureApiDefaults(this WebApplicationBuilder builder, ApiDefaultsOptions options)
    {
        options.ServiceName = "brand-workspace";
        options.EnableOtlpExporters = false;
        options.EnableRateLimiting = false;
        options.EnableOutputCache = false;
        options.EnableCrossOriginEmbedderPolicy = false;
        options.EnableHttpsRedirection = false;
        options.SpaHosting = _ => { };
        // Behind the TLS-terminating ingress, sign-in needs the forwarded scheme and host; any other sender keeps its
        // own.
        foreach (var proxy in builder.Configuration.GetSection("Deployment:TrustedProxies").Get<string[]>() ?? [])
            options.TrustedProxies.Add(proxy);
    }

    /// <summary>Registers the PostgreSQL pool, the SDK connection factory and Dapper's conventions.</summary>
    private static WebApplicationBuilder AddPostgresDatabase(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton(services => NpgsqlDataSource.Create(
            services.GetRequiredService<IConfiguration>().GetConnectionString("Database")
            ?? throw new InvalidOperationException("ConnectionStrings:Database is required.")));
        builder.Services.AddSingleton<DbDataSource>(services => services.GetRequiredService<NpgsqlDataSource>());
        builder.Services.AddDataSourceConnectionFactory();
        builder.Services.AddDapperConventions();
        return builder;
    }

    /// <summary>
    /// Registers foundation diagnostics, the schema migrator (the boot's and the <c>migrate</c> verb's) and the
    /// readiness check <c>/health</c> reports.
    /// </summary>
    private static WebApplicationBuilder AddFoundation(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IFoundationRepository, FoundationRepository>();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddDatabaseBespokeMigrations(typeof(FoundationRepository).Assembly, options =>
        {
            options.Version = BuildIdentity.ProductVersion;
            options.Release = BuildIdentity.Release;
            options.AdvisoryLockId = 8_264_001;
        });
        // Desk's own repository answers readiness: PostgreSQL reachable and Desk's schema in place.
        builder.Services.AddHealthChecks().AddCheck<SchemaReadinessHealthCheck>("database", tags: ["ready"]);
        return builder;
    }

    /// <summary>
    /// Registers sign-in through Ocharo's Identity instance (BFF, section <c>Identity:Client</c>), the Desk projection
    /// of its people, default deny, and the verification of its service tokens.
    /// </summary>
    /// <remarks>
    /// The import commands resolve <c>--actor</c> against the same projection; Desk keeps no accounts of its own since
    /// migration <c>013-identity-cutover</c>.
    /// </remarks>
    private static WebApplicationBuilder AddAccess(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IAccessRepository, AccessRepository>();
        builder.Services.AddIdentityBff();
        // The BFF keeps sign-in sessions in IDistributedCache: a table in Desk's own database, so a restart or deploy
        // keeps everyone signed in; its `distributed_cache` set migrates beside Desk's own.
        builder.Services.AddPostgresDistributedCache();
        builder.Services.AddOptions<PostgresDistributedCacheOptions>()
            .Configure<IConfiguration>((options, configuration) =>
                options.ConnectionString = configuration.GetConnectionString("Database") ?? string.Empty);
        builder.Services.AddDefaultDenyAuthorization(BffAuthenticationSchemeConstants.Cookie);
        builder.AddServiceTokens();
        // The key ring and its name stay as deployed, or every session is signed out; a local run keeps its machine
        // folder. Resolved from the final configuration, after test and deployment overrides.
        builder.Services.AddPersistentDataProtection(_ => { });
        builder.Services.AddOptions<PersistentDataProtectionOptions>()
            .Configure<IConfiguration, IHostEnvironment>((options, configuration, environment) =>
            {
                options.ApplicationName = configuration["DataProtection:ApplicationName"]
                    ?? $"brand-workspace:{environment.EnvironmentName}";
                options.KeyDirectory = configuration["DataProtection:KeysPath"] is { } path
                    && !string.IsNullOrWhiteSpace(path)
                    ? path
                    : environment.IsLocalRun(configuration)
                        ? Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                            "brand-workspace",
                            environment.EnvironmentName,
                            "keys")
                        : null;
            });
        return builder;
    }

    /// <summary>
    /// Verifies service tokens offline against the JWKS of Ocharo's Identity instance: the one trusted issuer is
    /// <c>Identity:Client:Authority</c> and the one audience <c>Identity:Resource:Uri</c>, each as
    /// <see cref="Uri.AbsoluteUri"/>.
    /// </summary>
    /// <remarks>
    /// The bearer registration takes both values now, so a test host passes them as host configuration. A missing or
    /// malformed value registers no scheme and refuses the boot; a command that never boots, such as the media probe of
    /// the image build, runs without them.
    /// </remarks>
    private static void AddServiceTokens(this WebApplicationBuilder builder)
    {
        builder.Services.AddValidatedSettings<IdentityResourceSettings>(
            builder.Configuration,
            IdentityResourceSettings.SectionName,
            rules => rules.ValidateSetting(
                nameof(IdentityResourceSettings.Uri),
                "absolute-uri",
                settings => IdentityResourceSettings.Normalize(settings.Uri) is not null,
                $"{IdentityResourceSettings.SectionName}:{nameof(IdentityResourceSettings.Uri)} must be an absolute "
                + "HTTPS URI (HTTP only on a loopback host) without a fragment."));
        var isHttpsRequired = builder.Configuration.GetValue("Identity:Client:IsHttpsMetadataRequired", true);
        if (!Uri.TryCreate(builder.Configuration["Identity:Client:Authority"], UriKind.Absolute, out var authority)
            || (isHttpsRequired && authority.Scheme != Uri.UriSchemeHttps)
            || IdentityResourceSettings.Normalize(builder.Configuration["Identity:Resource:Uri"]) is not { } audience)
            return;
        var issuer = authority.AbsoluteUri;
        builder.Services.AddJwtBearerAuthentication(options =>
        {
            options.Audience = audience;
            options.TrustedIssuers.Add(new TrustedIssuer { Issuer = issuer });
            options.IsInsecureMetadataAllowedInDevelopment = !isHttpsRequired;
        });
        builder.Services.AddTokenDenyList();
        var signalUrlKey = $"{IdentityResourceSettings.SectionName}:{nameof(IdentityResourceSettings.SignalUrl)}";
        if (builder.Configuration[signalUrlKey] is { Length: > 0 } signalUrl)
        {
            builder.Services.AddIdentitySignalReceiver(options =>
            {
                options.Audience = signalUrl;
                options.TrustedIssuers.Add(new TrustedIssuer { Issuer = issuer });
                options.IsInsecureMetadataAllowedInDevelopment = !isHttpsRequired;
            });
        }
    }

    /// <summary>
    /// Holds the root URL of the WoW2 Files instance Desk stores its media in; unset, every byte stays in PostgreSQL.
    /// </summary>
    private const string FilesBaseUrlKey = "Files:BaseUrl";

    /// <summary>
    /// Registers Desk's file store: WoW2 Files over HTTP when <see cref="FilesBaseUrlKey"/> is set, by tokens of Desk's
    /// own Identity client <c>desk</c> (a person's exchanged within their BFF session, Desk's own client credentials
    /// for the import CLI, the boot backfill and anonymous routes), else none. Both read at resolve time, so a test
    /// host's settings decide (research/files-adoption.md #13, #15).
    /// </summary>
    /// <remarks>
    /// The BFF registered by <see cref="AddAccess"/> supplies the person's token: its subject-token source reads the
    /// signed-in session, so the Files client exchanges it without a bearer header.
    /// </remarks>
    private static WebApplicationBuilder AddDeskFiles(this WebApplicationBuilder builder)
    {
        builder.Services.AddFilesClient(_ => { });
        builder.Services.AddOptions<FilesClientOptions>().Configure<IConfiguration>((files, configuration) =>
        {
            if (configuration[FilesBaseUrlKey] is not { Length: > 0 } url)
                return;
            files.BaseAddress = new Uri(url);
            if (!Uri.TryCreate(configuration["Identity:Client:Authority"], UriKind.Absolute, out var authority))
                return;
            files.Identity = new FilesClientIdentityOptions
            {
                TokenEndpoint = new Uri(authority, "connect/token"),
                ClientId = configuration["Identity:Client:ClientId"] ?? string.Empty,
                ClientSecret = configuration["Identity:Client:ClientSecret"] ?? string.Empty,
            };
        });
        builder.Services.AddScoped<FilesDeskStore>();
        builder.Services.AddScoped<IDeskFileStore>(provider => UsesFiles(provider)
            ? provider.GetRequiredService<FilesDeskStore>()
            : NoDeskFiles.Instance);
        builder.Services.AddScoped<IDeskFileIdRepository, DeskFileIdRepository>();
        builder.Services.AddScoped<DeskFilesBackfill>();
        return builder;
    }

    /// <summary>Tells whether this run stores media in WoW2 Files.</summary>
    private static bool UsesFiles(IServiceProvider provider) =>
        provider.GetRequiredService<IConfiguration>()[FilesBaseUrlKey] is { Length: > 0 };

    /// <summary>
    /// Fills the file ids of rows the migration moved before the API listens. An unreachable or slow Files leaves those
    /// rows on their PostgreSQL bytes and never fails the boot; the next boot tries again.
    /// </summary>
    private static async Task BackfillFileIdsAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        if (!UsesFiles(scope.ServiceProvider)) return;
        using var limit = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        try
        {
            await scope.ServiceProvider.GetRequiredService<DeskFilesBackfill>().RunAsync(limit.Token);
        }
        catch (Exception exception) when (exception is HttpRequestException
                                              or OperationCanceledException
                                              or InvalidOperationException
                                              or AppException)
        {
            app.Logger.LogWarning(exception, "Desk's file ids stay unfilled: WoW2 Files could not be reached at boot.");
        }
    }

    /// <summary>Registers the organization-scoped content preparation workflow.</summary>
    private static WebApplicationBuilder AddPreparation(this WebApplicationBuilder builder)
    {
        // Scoped, as its Files store is: a person's request calls Files with that person's token.
        builder.Services.AddScoped<IPreparationRepository, PreparationRepository>();
        builder.Services.AddSingleton<IManualPackageBuilder, ManualPackageBuilder>();
        builder.Services.AddSingleton<ImageValidationService>();
        builder.Services.AddScoped<PreparationService>();
        return builder;
    }

    /// <summary>Registers durable editor documents and their validation workflow.</summary>
    private static WebApplicationBuilder AddDesignDocuments(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IDesignDocumentRepository, DesignDocumentRepository>();
        builder.Services.AddSingleton<DesignDocumentValidationService>();
        builder.Services.AddScoped<DesignDocumentService>();
        return builder;
    }

    /// <summary>Registers commerce persistence and preserved source adoption.</summary>
    private static WebApplicationBuilder AddCommerce(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<ICommerceRules, CommerceRuleSet>();
        builder.Services.AddSingleton<ICommerceRepository, CommerceRepository>();
        builder.Services.AddSingleton<LegacyCommerceReader>();
        builder.Services.AddScoped<CommerceMediaFiles>();
        return builder;
    }

    /// <summary>Registers supplier records, contact routes and bounded content-addressed supplier media.</summary>
    private static WebApplicationBuilder AddSourcing(this WebApplicationBuilder builder)
    {
        builder.Services.AddImageProcessing(options =>
        {
            options.MaxInputBytes = SourcingMediaInspector.MaximumImageBytes;
            options.MaxPixels = SourcingMediaInspector.MaximumPixels;
        });
        builder.Services.AddSingleton<ISourcingRepository, SourcingRepository>();
        builder.Services.AddSingleton<SourcingValidationService>();
        builder.Services.AddSingleton<SourcingMediaInspector>();
        builder.Services.AddScoped<SourcingService>();
        builder.Services.AddScoped<SourcingImportService>();
        return builder;
    }

    /// <summary>Registers the showcase events feed and its explicit bundle import.</summary>
    private static WebApplicationBuilder AddEvents(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IEventRepository, EventRepository>();
        builder.Services.AddSingleton<EventValidationService>();
        builder.Services.AddScoped<EventService>();
        builder.Services.AddScoped<EventImportService>();
        return builder;
    }

    /// <summary>Registers the optional image worker from explicit server configuration.</summary>
    private static WebApplicationBuilder AddMediaProcessing(this WebApplicationBuilder builder)
    {
        builder.Services.AddBackgroundRemoval(options =>
        {
            options.BaseUrl = builder.Configuration["ImageProcessing:WorkerBaseUrl"];
            options.ApiKey = builder.Configuration["ImageProcessing:WorkerKey"];
            // The worker is a sibling container on the platform's private network, which carries the ingress's own
            // plain HTTP.
            options.AllowPrivateNetworkHttp = true;
        });
        // The bearer scheme alone, so Desk's BFF cookie never opens these routes.
        builder.Services.AddAuthorizationBuilder().AddPolicy(MediaProcessingPolicy.Name, policy => policy
            .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
            .RequireScope(MediaProcessingPolicy.Scope));
        return builder;
    }

    /// <summary>
    /// Registers the public runtime settings the SPA reads at startup: the Ocharo app and site addresses.
    /// </summary>
    private static WebApplicationBuilder AddRuntimeConfig(this WebApplicationBuilder builder)
    {
        // Every address is optional, and a malformed one is left out rather than failing the host over a menu link.
        builder.Services.AddValidatedSettings<OcharoAppsSettings>(
            builder.Configuration,
            OcharoAppsSettings.SectionName,
            _ => { });
        return builder;
    }

    /// <summary>
    /// Maps the Identity session routes, and the revocation signals route when <c>Identity:Resource:SignalUrl</c> is
    /// set.
    /// </summary>
    private static WebApplication MapIdentityRoutes(this WebApplication app)
    {
        app.MapIdentityBffEndpoints();
        var signalUrlKey = $"{IdentityResourceSettings.SectionName}:{nameof(IdentityResourceSettings.SignalUrl)}";
        if (!string.IsNullOrEmpty(app.Configuration[signalUrlKey]))
            app.MapIdentitySignals(IdentityResourceSettings.SignalPath);
        return app;
    }

    /// <summary>Maps the controllers and warns once about any runtime-config address left out.</summary>
    private static WebApplication MapControllerEndpoints(this WebApplication app)
    {
        var rejected = app.Services.GetRequiredService<OcharoAppsSettings>().RejectedSettings();
        if (rejected.Count > 0)
        {
            app.Logger.LogWarning(
                "Runtime config leaves out {Settings}: each must be a plain HTTPS address.",
                string.Join(", ", rejected));
        }
        app.MapControllers();
        return app;
    }

    /// <summary>
    /// Registers the mediator over the handler-bearing assembly: exception-to-result outermost, then logging.
    /// </summary>
    private static WebApplicationBuilder AddMediator(this WebApplicationBuilder builder)
    {
        builder.Services.AddMediator(typeof(InfrastructureAssembly).Assembly);
        builder.Services.AddMediatorLoggingInterceptor();
        return builder;
    }

    /// <summary>
    /// Registers the HTTP surface: problem details with Desk's status mapping, and the controllers on the SDK wire
    /// contract.
    /// </summary>
    /// <remarks>
    /// A missing body member binds as null, as before, for the handlers to refuse with their own message.
    /// </remarks>
    private static WebApplicationBuilder AddHttpSurface(this WebApplicationBuilder builder)
    {
        builder.Services.AddHttpProblemDetails();
        builder.Services.AddControllersWithSdkJson()
            .AddMvcOptions(options => options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true);
        return builder;
    }

    /// <summary>
    /// Configures the HTTP pipeline: Desk's JSON errors, then the SDK's (forwarded headers, secure headers,
    /// compression, the SPA bundle, <c>/health</c> and the SPA fallback, whose unknown <c>/api</c> paths answer a JSON
    /// 404), with Desk's sign-in in its identity slot after routing. Public brand routes stay embeddable cross-origin
    /// (<see cref="Commerce.Controllers.PublicBrandsController"/>).
    /// </summary>
    private static WebApplication UseHttpSurface(this WebApplication app)
    {
        app.UseHttpProblemDetails();
        app.UseApiDefaults(identity =>
        {
            identity.Use(async (context, next) =>
            {
                if (context.Request.Path.StartsWithSegments("/api/identity")
                    || context.Request.Path.StartsWithSegments("/api/organizations"))
                {
                    context.Response.OnStarting(() =>
                    {
                        context.Response.Headers.CacheControl = "no-store";
                        context.Response.Headers.Pragma = "no-cache";
                        return Task.CompletedTask;
                    });
                    if (!context.Request.IsHttps)
                    {
                        await AccessProblems.Create(400, "HTTPS is required", "https_required").ExecuteAsync(context);
                        return;
                    }
                }
                await next(context);
            });
            identity.UseIdentityBff();
        });
        return app;
    }

    /// <summary>Applies the schema before the API listens, so readiness never precedes it.</summary>
    /// <remarks>An unreachable database is retried ten times, two seconds apart; any other failure, or the last
    /// attempt, aborts startup so a rollout stops on an unmigrated schema.</remarks>
    private static async Task MigrateOnStartupAsync(this WebApplication app)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                // Desk's own set, then every set a module registers (the session cache's `distributed_cache`).
                var result = await app.Services.ApplyMigrationSetsAsync(
                    "startup",
                    ct: app.Lifetime.ApplicationStopping);
                if (result is not Result<IReadOnlyList<MigrationSetApplied>>.Success success)
                {
                    throw new InvalidOperationException(
                        $"Migration failed ({result}); inspect schema history and source integrity.");
                }
                app.Logger.LogInformation(
                    "Schema ready; {Count} migrations applied at startup.",
                    success.Value.Sum(set => set.Applied.Count));
                return;
            }
            catch (Exception exception)
                when (attempt < 10 && exception is NpgsqlException { IsTransient: true } or TimeoutException)
            {
                app.Logger.LogWarning(
                    "Startup migration attempt {Attempt} could not reach the database; retrying.",
                    attempt);
                await Task.Delay(TimeSpan.FromSeconds(2), app.Lifetime.ApplicationStopping);
            }
        }
    }
}
