using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Npgsql;
using OcharoBrand.Api.Brand;
using OcharoBrand.Api.RuntimeConfig.Settings;
using OcharoBrand.Application.Brand.Repositories;
using OcharoBrand.Application.Brand.Services;
using OcharoBrand.Infrastructure;
using OcharoBrand.Infrastructure.Brand;
using OcharoBrand.Infrastructure.Brand.FoundationServices;
using OcharoBrand.Persistence;
using OcharoBrand.Persistence.Brand;
using OcharoBrand.Persistence.Brand.Mappers;
using OcharoBrand.Persistence.Import;
using WoW.Two.Sdk.Beta.Be.Data.Abstractions;
using WoW.Two.Sdk.Beta.Be.Data.EntityFrameworkCore;
using WoW.Two.Sdk.Beta.Be.Data.Migrations.Bespoke;
using WoW.Two.Sdk.Beta.Be.Foundation.Configuration;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Options;
using WoW.Two.Sdk.Beta.Be.Mediator;
using WoW.Two.Sdk.Beta.Be.Observability.HealthChecks;
using WoW.Two.Sdk.Beta.Be.Storage.Core;
using WoW.Two.Sdk.Beta.Be.Storage.FileSystem;
using WoW.Two.Sdk.Beta.Be.Web.Json;
using WoW.Two.Sdk.Beta.Be.Web.ProblemDetails;
using WoW.Two.Sdk.Beta.Be.Identity.Bff;
using WoW.Two.Sdk.Beta.Be.Files.Client;
using WoW.Two.Sdk.Beta.Be.Meta;

namespace OcharoBrand.Api.Configurations;

public static partial class HostConfiguration
{
    /// <summary>Registers Brand's PostgreSQL storage in the product database (<c>ConnectionStrings:Database</c>, filled per
    /// target by Wheelhouse): the <c>brand</c> migration set in schema <c>brand</c>, the context over it with snake_case
    /// names, its readiness check on the bundle's <c>/health</c>, and the import of the retired SQLite store.</summary>
    /// <remarks>Brand stores no organization or account id, so its set keys into no Identity table and depends on none.</remarks>
    private static WebApplicationBuilder AddPostgresDatabase(this WebApplicationBuilder builder)
    {
        // The configured string, not the data source's: a data source drops the password from its own copy.
        static string ConnectionString(IServiceProvider provider) =>
            provider.GetRequiredService<IConfiguration>().GetConnectionString(BrandDatabaseConstants.ConnectionStringName)
            ?? throw new InvalidOperationException($"ConnectionStrings:{BrandDatabaseConstants.ConnectionStringName} is required.");
        builder.Services.AddSingleton(provider => NpgsqlDataSource.Create(ConnectionString(provider)));
        builder.Services.AddKeyedSingleton<IDbConnectionFactory>(BrandDatabaseConstants.MigrationSetName, static (provider, _) =>
            new DataSourceConnectionFactory(provider.GetRequiredService<NpgsqlDataSource>()));
        builder.Services.AddKeyedSingleton(BrandDatabaseConstants.MigrationSetName, static (provider, _) =>
            new DatabaseSettings { ConnectionString = ConnectionString(provider) });
        builder.Services.AddDatabaseBespokeMigrationSet(
            BrandDatabaseConstants.MigrationSetName,
            static _ => new EmbeddedResourceMigrationBroker(typeof(OcharoBrandDbContext).Assembly, BrandDatabaseConstants.MigrationResourcePrefix),
            static migration =>
            {
                migration.SchemaName = BrandDatabaseConstants.Schema;
                migration.SchemaOwner = "product";
                migration.AdvisoryLockId = BrandDatabaseConstants.AdvisoryLockId;
                migration.Version = BuildIdentity.MigrationVersion;
                migration.Release = BuildIdentity.MigrationRelease;
            });
        builder.Services.AddDbContext<OcharoBrandDbContext>((provider, options) =>
            options.UseNpgsql(provider.GetRequiredService<NpgsqlDataSource>()).UseSnakeCaseNamingConvention());
        builder.Services.AddHealthChecks().AddDatabaseReadinessCheck<OcharoBrandDbContext>();
        builder.AddLegacySqlite();
        return builder;
    }

    /// <summary>Registers where the retired SQLite store, its key ring and its blob folder sit, from today's settings, and
    /// the one-time import that copies them; relative paths resolve against the content root.</summary>
    /// <remarks>The key ring defaults to today's <c>DataProtection:KeysPath</c>, else a local run's machine folder.</remarks>
    private static WebApplicationBuilder AddLegacySqlite(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton(provider =>
        {
            var configuration = provider.GetRequiredService<IConfiguration>();
            var environment = provider.GetRequiredService<IHostEnvironment>();
            var configured = configuration.GetSection(LegacySqliteSettings.SectionName).Get<LegacySqliteSettings>() ?? new LegacySqliteSettings();
            var keys = configuration[$"{LegacySqliteSettings.SectionName}:{nameof(LegacySqliteSettings.KeysPath)}"]
                ?? configuration["DataProtection:KeysPath"]
                ?? (environment.IsLocalRun(configuration) ? LocalKeyRing(environment) : configured.KeysPath);
            var blobs = configuration[$"{LegacySqliteSettings.SectionName}:{nameof(LegacySqliteSettings.BlobsPath)}"]
                ?? Path.Combine(provider.GetRequiredService<IOptions<BrandOptions>>().Value.DataRoot, "blobs");
            var root = environment.ContentRootPath;
            return configured with
            {
                Path = Path.GetFullPath(configured.Path, root),
                KeysPath = Path.GetFullPath(keys, root),
                BlobsPath = Path.GetFullPath(blobs, root),
            };
        });
        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<LegacySqliteImport>();
        return builder;
    }

    /// <summary>The machine folder a local run kept its key ring in before the move.</summary>
    private static string LocalKeyRing(IHostEnvironment environment) =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ocharo-brand", environment.EnvironmentName, "keys");

    /// <summary>Registers brand identity, media, the draft/release handlers behind the mediator and their persistence adapters;
    /// a save that loses to a concurrent one answers a conflict.</summary>
    private static WebApplicationBuilder AddBrand(this WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<IBrandRepository, BrandRepository>();
        builder.Services.AddExceptionMappingRule<PostgresBusyExceptionMappingRule>();
        builder.Services.AddOptions<BrandOptions>().Bind(builder.Configuration.GetSection(BrandOptions.Section));
        // Resolve final options after host/test overrides; retain the host content-root anchor.
        var contentRoot = builder.Environment.ContentRootPath;
        builder.Services.AddSingleton<IBlobRepository>(provider =>
        {
            var dataRoot = provider.GetRequiredService<IOptions<BrandOptions>>().Value.DataRoot;
            var root = Path.IsPathRooted(dataRoot) ? dataRoot : Path.Combine(contentRoot, dataRoot);
            return new LocalFileBlobRepository(Path.Combine(root, "blobs"));
        });
        builder.AddBrandFiles();
        builder.Services.AddScoped<BrandDraftService>();
        builder.Services.AddScoped<PublicBrandService>();
        builder.Services.AddScoped<BrandSeeder>();
        // The SDK registration converts a handler's thrown failure into its AppResult, outermost.
        builder.Services.AddMediator(typeof(InfrastructureAssembly).Assembly);
        return builder;
    }

    /// <summary>Holds the root URL of the WoW2 Files instance Brand stores its media in; unset, media stay in the local
    /// blob folder.</summary>
    private const string FilesBaseUrlKey = "Files:BaseUrl";

    /// <summary>Registers the media store: WoW2 Files over HTTP when <see cref="FilesBaseUrlKey"/> is set, by tokens of
    /// Brand's own Identity client (a person's exchanged within their session, Brand's own otherwise), else the local blob
    /// folder. Both read at resolve time, so a test host's settings decide (research/files-adoption.md #23).</summary>
    private static WebApplicationBuilder AddBrandFiles(this WebApplicationBuilder builder)
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
        builder.Services.AddScoped<MediaStore>();
        builder.Services.AddScoped<FilesMediaStore>();
        builder.Services.AddScoped<FilesMediaBackfill>();
        builder.Services.AddScoped<IMediaStore>(provider => UsesFiles(provider)
            ? provider.GetRequiredService<FilesMediaStore>()
            : provider.GetRequiredService<MediaStore>());
        return builder;
    }

    /// <summary>Tells whether this run stores media in WoW2 Files.</summary>
    private static bool UsesFiles(IServiceProvider provider) =>
        provider.GetRequiredService<IConfiguration>()[FilesBaseUrlKey] is { Length: > 0 };

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
    /// the client accepts, with Brand's statuses for the failures no SDK kind carries; the boot floor holds forwarded headers
    /// (loopback and <c>Deployment:TrustedProxies</c>) and OpenAPI.</summary>
    private static WebApplicationBuilder AddHttpSurface(this WebApplicationBuilder builder)
    {
        builder.Services.AddHttpProblemDetails();
        builder.Services.AddControllersWithSdkJson();
        return builder;
    }

    /// <summary>Configures the SDK pipeline — secure headers, compression, the operator SPA, <c>/health</c> and the SPA
    /// fallback, whose unknown <c>/api</c> routes answer a JSON 404 — with SDK problem details, Brand's headers and access
    /// between routing and the endpoints, then the brand API and the Identity session routes. Static files, the SPA fallback and
    /// the public routes (<see cref="Access.BrandAccess.IsPublic"/>) answer anonymously; an unknown private route needs a
    /// session too, so it discloses nothing to an anonymous caller.</summary>
    private static WebApplication ConfigureHttpSurface(this WebApplication app)
    {
        app.UseApiDefaults(pipeline =>
        {
            pipeline.UseHttpProblemDetails();
            pipeline.UseMiddleware<BrandResponseHeadersMiddleware>();
            pipeline.UseAccess();
            pipeline.UseMiddleware<BrandStorageMovingMiddleware>();
        });
        app.WarnAboutRejectedRuntimeConfig();
        app.MapControllers();
        app.MapIdentityBffEndpoints();
        return app;
    }

    /// <summary>Seeds an empty brand and fills file ids before traffic starts; while the retired SQLite store waits for
    /// <c>import-sqlite</c>, neither runs, so the copy finds PostgreSQL as the migration left it.</summary>
    private static async Task<WebApplication> InitializeBrandAsync(this WebApplication app)
    {
        if (app.Services.GetRequiredService<LegacySqliteSettings>().Waiting)
        {
            app.Logger.LogWarning("Brand's SQLite store waits for `import-sqlite`; brand routes answer 503 until it is copied.");
            return app;
        }

        using var scope = app.Services.CreateScope();
        if (UsesFiles(scope.ServiceProvider))
            await scope.ServiceProvider.BackfillFileIdsAsync(app.Logger);
        await scope.ServiceProvider.GetRequiredService<BrandSeeder>().SeedIfEmptyAsync(CancellationToken.None);
        return app;
    }

    /// <summary>Fills the file ids the migration made; an unreachable Files leaves those files on local bytes, never the boot.</summary>
    private static async Task BackfillFileIdsAsync(this IServiceProvider services, ILogger logger)
    {
        try
        {
            await services.GetRequiredService<FilesMediaBackfill>().RunAsync(CancellationToken.None);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            logger.LogWarning(exception, "Brand's file ids stay unfilled: WoW2 Files could not be reached at boot.");
        }
    }
}
