using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OcharoBrand.Api.Brand;
using OcharoBrand.Api.Brand.Mappers;
using OcharoBrand.Api.RuntimeConfig.Settings;
using OcharoBrand.Application.Brand.Repositories;
using OcharoBrand.Application.Brand.Services;
using OcharoBrand.Infrastructure;
using OcharoBrand.Infrastructure.Brand;
using OcharoBrand.Infrastructure.Brand.FoundationServices;
using OcharoBrand.Persistence;
using OcharoBrand.Persistence.Brand;
using OcharoBrand.Persistence.Brand.Mappers;
using WoW.Two.Sdk.Beta.Be.Foundation.Configuration;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Options;
using WoW.Two.Sdk.Beta.Be.Mediator;
using WoW.Two.Sdk.Beta.Be.Observability.HealthChecks;
using WoW.Two.Sdk.Beta.Be.Storage.Core;
using WoW.Two.Sdk.Beta.Be.Storage.FileSystem;
using WoW.Two.Sdk.Beta.Be.Web.ErrorMapping;
using WoW.Two.Sdk.Beta.Be.Web.Json;
using WoW.Two.Sdk.Beta.Be.Web.ProblemDetails;
using WoW.Two.Sdk.Beta.Be.Identity.Bff;
using WoW.Two.Sdk.Beta.Be.Files.Client;
using WoW.Two.Sdk.Beta.Be.Meta;

namespace OcharoBrand.Api.Configurations;

public static partial class HostConfiguration
{
    /// <summary>Registers the SQLite context with final host configuration and content-root-relative storage, and its
    /// readiness check on the bundle's <c>/health</c>.</summary>
    private static WebApplicationBuilder AddSqliteDatabase(this WebApplicationBuilder builder)
    {
        builder.Services.AddDbContext<OcharoBrandDbContext>((provider, options) =>
        {
            var raw = (provider.GetService<IConfiguration>() ?? builder.Configuration)["ConnectionStrings:OcharoBrand"]
                ?? "Data Source=App_Data/ocharo-brand.db";
            var connection = new SqliteConnectionStringBuilder(raw);
            var contentRoot = provider.GetService<IHostEnvironment>()?.ContentRootPath;
            if (connection.DataSource is { Length: > 0 } file && file != ":memory:" && !Path.IsPathRooted(file) && contentRoot is not null)
                connection.DataSource = Path.Combine(contentRoot, file);
            options.UseSqlite(connection.ToString());
        });
        builder.Services.AddHealthChecks().AddDatabaseReadinessCheck<OcharoBrandDbContext>();
        return builder;
    }

    /// <summary>Registers brand identity, media, the draft/release handlers behind the mediator and their persistence adapters;
    /// a save that meets SQLite's lock answers a conflict.</summary>
    private static WebApplicationBuilder AddBrand(this WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<IBrandRepository, BrandRepository>();
        builder.Services.AddExceptionMappingRule<SqliteBusyExceptionMappingRule>();
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
    /// folder. Both read at resolve time, so a test host's settings decide (planning/files-adoption.md #23).</summary>
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
        builder.Services.AddSingleton<IErrorHttpStatusCodeMapper, BrandErrorHttpStatusCodeMapper>();
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
        });
        if (app.Environment.IsLocalRun(app.Configuration))
            app.MapOpenApi().AllowAnonymous();
        app.WarnAboutRejectedRuntimeConfig();
        app.MapControllers();
        app.MapIdentityBffEndpoints();
        return app;
    }

    /// <summary>Configures mandatory brand database preparation and seeds an empty brand before traffic starts.</summary>
    private static async Task<WebApplication> InitializeBrandAsync(this WebApplication app)
    {
        await app.Services.InitializeBrandDatabaseAsync();
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

    /// <summary>Configures the SQLite schema and write-ahead log before brand seeding.</summary>
    private static async Task InitializeBrandDatabaseAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OcharoBrandDbContext>();
        var source = new SqliteConnectionStringBuilder(db.Database.GetConnectionString()).DataSource;
        if (!string.IsNullOrEmpty(source) && source != ":memory:" && Path.GetDirectoryName(Path.GetFullPath(source)) is { } folder)
            Directory.CreateDirectory(folder);
        await db.Database.EnsureCreatedAsync();
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
        // A store made before WoW2 Files lacks the file-id column; EnsureCreated never alters an existing table.
        var hasFileId = await db.Database
            .SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM pragma_table_info('media_blobs') WHERE name = 'FileId'")
            .SingleAsync();
        if (hasFileId == 0)
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE media_blobs ADD COLUMN FileId TEXT NULL;");
    }
}
