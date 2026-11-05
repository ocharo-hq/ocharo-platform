using WoW.Two.Sdk.Beta.Be.Foundation.Configuration;
using WoW.Two.Sdk.Beta.Be.Foundation.Options;
using WoW.Two.Sdk.Beta.Be.Web.Hosting;
using WoW.Two.Sdk.Beta.Be.Web.ProblemDetails;
using BrandWorkspace.Api.MediaProcessing;
using BrandWorkspace.Api.Commerce;
using BrandWorkspace.Api.DesignDocuments;
using BrandWorkspace.Api.Access;
using BrandWorkspace.Api.Foundation;
using BrandWorkspace.Infrastructure.Preparation.Services;
using BrandWorkspace.Application.Preparation.Repositories;
using BrandWorkspace.Application.Preparation.Services;
using BrandWorkspace.Persistence.Preparation.Repositories;
using BrandWorkspace.Application.DesignDocuments.Repositories;
using BrandWorkspace.Infrastructure.DesignDocuments.Services;
using BrandWorkspace.Persistence.DesignDocuments.Repositories;
using BrandWorkspace.Application.Commerce;
using BrandWorkspace.Infrastructure.Commerce;
using BrandWorkspace.Persistence.Commerce;
using BrandWorkspace.Api.Sourcing;
using BrandWorkspace.Application.Sourcing.Repositories;
using BrandWorkspace.Infrastructure.Sourcing.Services;
using BrandWorkspace.Persistence.Sourcing.Repositories;
using BrandWorkspace.Api.Events;
using BrandWorkspace.Api.RuntimeConfig.Settings;
using BrandWorkspace.Application.Events.Repositories;
using BrandWorkspace.Infrastructure.Events.Services;
using BrandWorkspace.Persistence.Events.Repositories;
using WoW.Two.Sdk.Beta.Be.Media.Images;
using WoW.Two.Sdk.Beta.Be.Media.BackgroundRemoval;
using BrandWorkspace.Api.Preparation;
using BrandWorkspace.Application.Access;
using BrandWorkspace.Persistence.Access;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WoW.Two.Sdk.Beta.Be.Identity.Authorization;
using WoW.Two.Sdk.Beta.Be.Identity.Bff;
using WoW.Two.Sdk.Beta.Be.Identity.Jwt;
using WoW.Two.Sdk.Beta.Be.Caching.Postgres;
using System.Data.Common;
using BrandWorkspace.Application.Foundation.Repositories;
using BrandWorkspace.Domain.Foundation.Models;
using BrandWorkspace.Persistence.Foundation.Repositories;
using Hangfire.PostgreSql;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Npgsql;
using WoW.Two.Sdk.Beta.Be.Data.Abstractions;
using WoW.Two.Sdk.Beta.Be.Data.Migrations.Bespoke;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace BrandWorkspace.Api.Configurations;

public static partial class HostConfiguration
{
    /// <summary>Registers the optional target and local settings files, then the permitted deployment environment inputs.</summary>
    private static WebApplicationBuilder AddExplicitEnvironment(this WebApplicationBuilder builder)
    {
        builder.AddSettingsFiles();
        foreach (var source in builder.Configuration.Sources.OfType<EnvironmentVariablesConfigurationSource>().ToArray())
            builder.Configuration.Sources.Remove(source);
        var values = new Dictionary<string, string?>();
        if (Environment.GetEnvironmentVariable("ConnectionStrings__Database") is { } connection)
            values["ConnectionStrings:Database"] = connection;
        if (Environment.GetEnvironmentVariable("ASPNETCORE_URLS") is { } urls) values["urls"] = urls;
        if (Environment.GetEnvironmentVariable("AllowedHosts") is { } allowedHosts) values["AllowedHosts"] = allowedHosts;
        foreach (var key in new[] { "ImageProcessing__WorkerBaseUrl", "ImageProcessing__WorkerKey", "Commerce__SourceRoot", "Commerce__AdoptionOrganizationId", "DataProtection__KeysPath", "DataProtection__ApplicationName",
                     "Kestrel__Certificates__Default__Path", "Kestrel__Certificates__Default__Password",
                     "Identity__Client__Authority", "Identity__Client__ClientId", "Identity__Client__ClientSecret", "Identity__Client__CookieName",
                     "Identity__Client__PostSignOutRedirectPath", "Identity__Client__Environment", "Identity__Resource__Uri" })
            if (Environment.GetEnvironmentVariable(key) is { } value) values[key.Replace("__", ":")] = value;
        builder.Configuration.AddInMemoryCollection(values);
        return builder;
    }

    /// <summary>Registers the PostgreSQL connection pool and SDK connection factory.</summary>
    private static WebApplicationBuilder AddPostgresDatabase(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton(services => NpgsqlDataSource.Create(
            services.GetRequiredService<IConfiguration>().GetConnectionString("Database")
            ?? throw new InvalidOperationException("ConnectionStrings:Database is required.")));
        builder.Services.AddSingleton<DbDataSource>(services => services.GetRequiredService<NpgsqlDataSource>());
        builder.Services.AddDataSourceConnectionFactory();
        return builder;
    }

    /// <summary>Registers foundation diagnostics and, for serving and the migration command, the schema migrator.</summary>
    private static WebApplicationBuilder AddFoundation(this WebApplicationBuilder builder, bool migrate)
    {
        builder.Services.AddSingleton<IFoundationRepository, FoundationRepository>();
        builder.Services.AddSingleton(TimeProvider.System);
        if (!migrate) return builder;
        builder.Services.AddDatabaseBespokeMigrations(typeof(FoundationRepository).Assembly, options =>
        {
            options.Version = BuildIdentity.ProductVersion;
            options.Release = BuildIdentity.Release;
            options.AdvisoryLockId = 8_264_001;
        });
        return builder;
    }

    /// <summary>Registers sign-in through Ocharo's Identity instance (BFF, section <c>Identity:Client</c>), the Desk projection of
    /// its people, default deny, and the verification of its service tokens.</summary>
    /// <remarks>The framework identity store stays for the import commands, which resolve <c>--actor</c> by email, until
    /// migration 011 retires Desk's own accounts.</remarks>
    private static WebApplicationBuilder AddAccess(this WebApplicationBuilder builder)
    {
        builder.Services.AddDbContext<AccessDbContext>((services, options) =>
            options.UseNpgsql(services.GetRequiredService<NpgsqlDataSource>()));
        builder.Services.AddIdentityCore<AccessUser>(options => options.User.RequireUniqueEmail = true)
            .AddEntityFrameworkStores<AccessDbContext>();
        builder.Services.AddSingleton<IAccessRepository, AccessRepository>();
        builder.Services.AddScoped<OrganizationAccessFilter>();
        builder.Services.AddIdentityBff();
        // The BFF keeps sign-in sessions in IDistributedCache: a table in Desk's own database, so a restart or deploy
        // keeps everyone signed in; its `distributed_cache` set migrates beside Desk's own.
        builder.Services.AddPostgresDistributedCache();
        builder.Services.AddOptions<PostgresDistributedCacheOptions>().Configure<IConfiguration>((options, configuration) =>
            options.ConnectionString = configuration.GetConnectionString("Database") ?? string.Empty);
        builder.Services.AddDefaultDenyAuthorization(BffAuthenticationSchemeConstants.Cookie);
        builder.AddServiceTokens();
        // Resolve final configuration after test/deployment overrides have been applied.
        builder.Services.AddDataProtection();
        builder.Services.AddOptions<Microsoft.AspNetCore.DataProtection.KeyManagement.KeyManagementOptions>()
            .Configure<IConfiguration, IHostEnvironment>((options, configuration, environment) =>
            {
                var path = configuration["DataProtection:KeysPath"];
                if (string.IsNullOrWhiteSpace(path))
                {
                    if (!environment.IsLocalRun(configuration)) throw new InvalidOperationException("DataProtection:KeysPath must name durable storage.");
                    path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "brand-workspace", environment.EnvironmentName, "keys");
                }
                options.XmlRepository = new Microsoft.AspNetCore.DataProtection.Repositories.FileSystemXmlRepository(
                    new DirectoryInfo(path), Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
            });
        builder.Services.AddOptions<DataProtectionOptions>().Configure<IConfiguration, IHostEnvironment>((options, configuration, environment) =>
            options.ApplicationDiscriminator = configuration["DataProtection:ApplicationName"] ?? $"brand-workspace:{environment.EnvironmentName}");
        return builder;
    }

    /// <summary>Verifies service tokens offline against the JWKS of Ocharo's Identity instance: the one trusted issuer is
    /// <c>Identity:Client:Authority</c> and the one audience <c>Identity:Resource:Uri</c>, each as <see cref="Uri.AbsoluteUri"/>.</summary>
    /// <remarks>The bearer registration takes both values now, so a test host passes them as host configuration. A missing or
    /// malformed value registers no scheme and refuses the boot; a command that never boots, such as the media probe of the
    /// image build, runs without them.</remarks>
    private static void AddServiceTokens(this WebApplicationBuilder builder)
    {
        builder.Services.AddValidatedSettings<IdentityResourceSettings>(builder.Configuration, IdentityResourceSettings.SectionName, rules => rules
            .ValidateSetting(nameof(IdentityResourceSettings.Uri), "absolute-uri", settings => IdentityResourceSettings.Normalize(settings.Uri) is not null,
                $"{IdentityResourceSettings.SectionName}:{nameof(IdentityResourceSettings.Uri)} must be an absolute HTTPS URI (HTTP only on a loopback host) without a fragment."));
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
        // The call above names its bearer scheme the default, which would sign people out of every plain
        // RequireAuthorization() route and open those routes to any service token; the BFF cookie stays Desk's default.
        builder.Services.AddAuthentication(BffAuthenticationSchemeConstants.Cookie);
    }

    /// <summary>Registers the organization-scoped content preparation workflow.</summary>
    private static WebApplicationBuilder AddPreparation(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IPreparationRepository, PreparationRepository>();
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
        builder.Services.AddSingleton<ICommerceRepository, CommerceRepository>();
        builder.Services.AddSingleton<LegacyCommerceReader>();
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
        });
        // The bearer scheme alone, so Desk's BFF cookie never opens these routes.
        builder.Services.AddAuthorizationBuilder().AddPolicy(MediaProcessingEndpoints.Policy, policy => policy
            .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme).RequireScope(MediaProcessingEndpoints.Scope));
        return builder;
    }

    /// <summary>Registers the public runtime settings the SPA reads at startup: the Ocharo app and site addresses.</summary>
    private static WebApplicationBuilder AddRuntimeConfig(this WebApplicationBuilder builder)
    {
        // Every address is optional, and a malformed one is left out rather than failing the host over a menu link.
        builder.Services.AddValidatedSettings<OcharoAppsSettings>(builder.Configuration, OcharoAppsSettings.SectionName, _ => { });
        builder.Services.AddControllers();
        return builder;
    }

    /// <summary>Maps the runtime-config controller and warns once about any address it leaves out.</summary>
    private static WebApplication MapRuntimeConfig(this WebApplication app)
    {
        var rejected = app.Services.GetRequiredService<OcharoAppsSettings>().RejectedSettings();
        if (rejected.Count > 0)
            app.Logger.LogWarning("Runtime config leaves out {Settings}: each must be a plain HTTPS address.", string.Join(", ", rejected));
        app.MapControllers();
        return app;
    }

    /// <summary>Registers the HTTP surface: forwarded headers trusted only from loopback and <c>Deployment:TrustedProxies</c>, and JSON errors.</summary>
    private static WebApplicationBuilder AddHttpSurface(this WebApplicationBuilder builder)
    {
        // Behind the TLS-terminating ingress, sign-in needs the forwarded scheme and host; any other sender keeps its own.
        var proxies = builder.Configuration.GetSection("Deployment:TrustedProxies").Get<string[]>() ?? [];
        builder.Services.AddProxyAwareHosting(options =>
        {
            foreach (var proxy in proxies)
                options.TrustedProxies.Add(proxy);
        });
        builder.Services.AddHttpProblemDetails();
        return builder;
    }

    /// <summary>Configures public technical status and SPA endpoints without business mutations.</summary>
    private static WebApplication AddFoundationEndpoints(this WebApplication app)
    {
        app.UseProxyAwareHosting();
        app.UseHttpProblemDetails();
        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.UseRouting();
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/api/identity") || context.Request.Path.StartsWithSegments("/api/organizations"))
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
        app.UseIdentityBff();
        app.MapGet("/api/system/version", (HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            return Results.Ok(new { service = "BrandWorkspace", version = BuildIdentity.Version });
        }).AllowAnonymous();
        app.MapGet("/health/live", () => Results.Ok(new { status = "alive" })).AllowAnonymous();
        app.MapGet("/health/ready", async (IFoundationRepository repository, CancellationToken token) =>
        {
            try
            {
                return await repository.IsReadyAsync(token) ? Results.Ok(new { status = "ready" })
                    : Results.Problem(statusCode: 503, title: "Database schema is not ready");
            }
            catch (Exception exception) when (exception is NpgsqlException or TimeoutException)
            {
                return Results.Problem(statusCode: 503, title: "Database schema is not ready");
            }
        }).AllowAnonymous();
        app.MapGet("/api/system/status", async (IFoundationRepository repository, TimeProvider time, CancellationToken token) =>
        {
            try
            {
                if (!await repository.IsReadyAsync(token))
                    return Results.Problem(statusCode: 503, title: "Database schema is not ready");
                var worker = WorkerSnapshot.From(await repository.GetLastHeartbeatAsync(token), time.GetUtcNow());
                return Results.Ok(new { service = "brand-workspace", stage = "foundation", persistence = "PostgreSQL", worker });
            }
            catch (Exception exception) when (exception is NpgsqlException or TimeoutException)
            {
                return Results.Problem(statusCode: 503, title: "Database schema is not ready");
            }
        }).AllowAnonymous();
        app.MapFallback("/api/{**path}", () => Results.Problem(statusCode: 404, title: "Not Found")).AllowAnonymous();
        app.MapFallbackToFile("index.html").AllowAnonymous();
        return app;
    }

    /// <summary>Applies pending schema migrations, then the job queue's provider-owned schema, and returns the labels applied.</summary>
    private static async Task<IReadOnlyList<string>> MigrateSchemaAsync(this WebApplication app, string appliedBy)
    {
        // Desk's own set, then every set a module registers (the session cache's `distributed_cache`).
        var result = await app.Services.ApplyMigrationSetsAsync(appliedBy);
        if (result is not Result<IReadOnlyList<MigrationSetApplied>>.Success success)
            throw new InvalidOperationException($"Migration failed ({result}); inspect schema history and source integrity.");
        // The queue's own installer is idempotent; the serving API registers no job client or server.
        await using var connection = await app.Services.GetRequiredService<NpgsqlDataSource>().OpenConnectionAsync();
        PostgreSqlObjectsInstaller.Install(connection);
        return [.. success.Value.SelectMany(set => set.Applied)];
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
                var applied = await app.MigrateSchemaAsync("startup");
                app.Logger.LogInformation("Schema ready; {Count} migrations applied at startup.", applied.Count);
                return;
            }
            catch (Exception exception) when (attempt < 10 && exception is NpgsqlException { IsTransient: true } or TimeoutException)
            {
                app.Logger.LogWarning("Startup migration attempt {Attempt} could not reach the database; retrying.", attempt);
                await Task.Delay(TimeSpan.FromSeconds(2));
            }
        }
    }

    /// <summary>Runs the explicit migration command, or applies the schema and starts the API; no path starts a worker.</summary>
    public static async Task RunCommandOrHostAsync(this WebApplication app, string[] args)
    {
        if (args.Contains("--verify-media-runtime"))
        {
            if (args.Length != 1) throw new ArgumentException("Use --verify-media-runtime alone.");
            try { MediaRuntimeVerification.Run(app.Services.GetRequiredService<ImageValidationService>()); }
            finally { await app.DisposeAsync(); }
            return;
        }
        if (args.Length == 1 && args[0] == "--migrate")
        {
            var migrated = await app.MigrateSchemaAsync("api-cli");
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { migrated, queueSchema = "ready" }));
            await app.DisposeAsync();
            return;
        }
        if (args.Contains("--migrate")) throw new ArgumentException("Use --migrate alone.");
        if (args.Contains("--import-sourcing"))
        {
            try { await SourcingImportCommand.RunAsync(app.Services, args); }
            finally { await app.DisposeAsync(); }
            return;
        }
        if (args.Contains("--import-events"))
        {
            try { await EventImportCommand.RunAsync(app.Services, args); }
            finally { await app.DisposeAsync(); }
            return;
        }
        await app.MigrateOnStartupAsync();
        await app.RunAsync();
    }
}
