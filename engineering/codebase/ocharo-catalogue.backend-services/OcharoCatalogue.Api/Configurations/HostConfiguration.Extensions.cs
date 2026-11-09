using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OcharoCatalogue.Api.RuntimeConfig.Settings;
using OcharoCatalogue.Application;
using OcharoCatalogue.Application.Abstractions;
using OcharoCatalogue.Application.Catalogue.Models;
using OcharoCatalogue.Application.Catalogue.Repositories;
using OcharoCatalogue.Application.Catalogue.Services;
using OcharoCatalogue.Infrastructure.Catalogue;
using OcharoCatalogue.Infrastructure.Time;
using OcharoCatalogue.Persistence;
using OcharoCatalogue.Persistence.Catalogue;
using WoW.Two.Sdk.Beta.Be.Foundation.Options;
using WoW.Two.Sdk.Beta.Be.Mediator;
using WoW.Two.Sdk.Beta.Be.Storage.Core;
using WoW.Two.Sdk.Beta.Be.Storage.FileSystem;
using WoW.Two.Sdk.Beta.Be.Web.Hosting;
using WoW.Two.Sdk.Beta.Be.Web.ProblemDetails;
using WoW.Two.Sdk.Beta.Be.Files.Client;

namespace OcharoCatalogue.Api.Configurations;

public static partial class HostConfiguration
{
    /// <summary>Registers the SQLite catalogue context with lazy host configuration.</summary>
    private static WebApplicationBuilder AddSqliteDatabase(this WebApplicationBuilder builder)
    {
        var configuration = builder.Configuration;
        builder.Services.AddDbContext<OcharoCatalogueDbContext>((provider, options) =>
        {
            var raw = (provider.GetService<IConfiguration>() ?? configuration)["ConnectionStrings:OcharoCatalogue"]
                ?? "Data Source=App_Data/ocharo-catalogue.db";
            var connection = new SqliteConnectionStringBuilder(raw);
            var contentRoot = provider.GetService<IHostEnvironment>()?.ContentRootPath;
            if (connection.DataSource is { Length: > 0 } file && file != ":memory:"
                && !Path.IsPathRooted(file) && contentRoot is not null)
                connection.DataSource = Path.Combine(contentRoot, file);
            options.UseSqlite(connection.ToString());
        });
        return builder;
    }

    /// <summary>Registers the shared catalogue clock.</summary>
    private static WebApplicationBuilder AddClock(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IClock, SystemClock>();
        return builder;
    }

    /// <summary>Registers catalogue settings, repositories, media, import and authoring services.</summary>
    private static WebApplicationBuilder AddCatalogue(this WebApplicationBuilder builder)
    {
        builder.Services.AddOptions<CatalogueOptions>()
            .Bind(builder.Configuration.GetSection(CatalogueOptions.Section));
        var contentRoot = builder.Environment.ContentRootPath;
        // Resolve options lazily so late host/test configuration overrides still apply.
        builder.Services.AddSingleton<IBlobRepository>(provider =>
        {
            var dataRoot = provider.GetRequiredService<IOptions<CatalogueOptions>>().Value.DataRoot;
            var root = Path.IsPathRooted(dataRoot) ? dataRoot : Path.Combine(contentRoot, dataRoot);
            return new LocalFileBlobRepository(Path.Combine(root, "blobs"));
        });
        builder.Services.AddScoped<ICatalogueRepository, CatalogueRepository>();
        builder.Services.AddScoped<IDesignRepository, CatalogueDesignRepository>();
        builder.AddCatalogueFiles();
        builder.Services.AddScoped<WorkspaceCatalogueImporter>();
        builder.Services.AddSingleton<IDesignArtifactValidator, DesignArtifactValidator>();
        builder.Services.AddScoped<SettingsService>();
        builder.Services.AddScoped<ProductService>();
        builder.Services.AddScoped<CollectionService>();
        builder.Services.AddScoped<PublicCatalogueService>();
        builder.Services.AddScoped<CatalogueMediaAccessService>();
        builder.Services.AddScoped<CatalogueExportService>();
        builder.Services.AddScoped<CatalogueDesignService>();
        builder.Services.AddScoped<CatalogueReleaseService>();
        return builder;
    }

    /// <summary>Registers the mediator with the explicit catalogue handler assembly.</summary>
    private static WebApplicationBuilder AddMediator(this WebApplicationBuilder builder)
    {
        builder.Services.AddMediator(typeof(ApplicationAssembly).Assembly);
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

    /// <summary>Registers forwarded headers trusted only from loopback and <c>Deployment:TrustedProxies</c>, the HTTP error
    /// boundary, controllers and OpenAPI.</summary>
    private static WebApplicationBuilder AddControllers(this WebApplicationBuilder builder)
    {
        // Behind the TLS-terminating ingress, same-origin write checks need the forwarded scheme; any other sender keeps its own.
        var proxies = builder.Configuration.GetSection("Deployment:TrustedProxies").Get<string[]>() ?? [];
        builder.Services.AddProxyAwareHosting(options =>
        {
            foreach (var proxy in proxies)
                options.TrustedProxies.Add(proxy);
        });
        builder.Services.AddHttpProblemDetails();
        builder.Services.AddControllers();
        builder.Services.AddOpenApi();
        return builder;
    }

    /// <summary>Holds the root URL of the WoW2 Files instance Catalogue stores its product images and release artifacts in;
    /// unset, they stay in the local blob folder and SQLite.</summary>
    private const string FilesBaseUrlKey = "Files:BaseUrl";

    /// <summary>Registers the media and release-artifact stores: WoW2 Files over HTTP when <see cref="FilesBaseUrlKey"/> is
    /// set, by tokens of Catalogue's own Identity client (a person's exchanged within their session, Catalogue's own
    /// otherwise), else the local blob folder and SQLite. Both read at resolve time, so a test host's settings decide
    /// (planning/files-adoption.md #24, #26).</summary>
    private static WebApplicationBuilder AddCatalogueFiles(this WebApplicationBuilder builder)
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
        builder.Services.AddScoped<LocalReleaseAssetStore>();
        builder.Services.AddScoped<FilesReleaseAssetStore>();
        builder.Services.AddScoped<IReleaseAssetStore>(provider => UsesFiles(provider)
            ? provider.GetRequiredService<FilesReleaseAssetStore>()
            : provider.GetRequiredService<LocalReleaseAssetStore>());
        return builder;
    }

    /// <summary>Tells whether this run stores product images and release artifacts in WoW2 Files.</summary>
    private static bool UsesFiles(IServiceProvider provider) =>
        provider.GetRequiredService<IConfiguration>()[FilesBaseUrlKey] is { Length: > 0 };

    /// <summary>Fills the file ids the migration made; an unreachable Files leaves those files on local bytes, never the boot.</summary>
    private static async Task BackfillFileIdsAsync(this IServiceProvider services, ILogger logger)
    {
        try
        {
            await services.GetRequiredService<FilesMediaBackfill>().RunAsync(CancellationToken.None);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            logger.LogWarning(exception, "Catalogue's file ids stay unfilled: WoW2 Files could not be reached at boot.");
        }
    }

    /// <summary>Configures catalogue storage and imports configured records into an empty catalogue.</summary>
    private static async Task InitializeCatalogueAsync(this WebApplication app)
    {
        await app.Services.InitializeDatabaseAsync();

        using var scope = app.Services.CreateScope();
        if (UsesFiles(scope.ServiceProvider))
            await scope.ServiceProvider.BackfillFileIdsAsync(app.Logger);
        var options = scope.ServiceProvider.GetRequiredService<IOptions<CatalogueOptions>>().Value;
        if (!options.SeedOnEmpty || string.IsNullOrWhiteSpace(options.SourceRoot)
            || !Directory.Exists(options.SourceRoot)) return;
        var repository = scope.ServiceProvider.GetRequiredService<ICatalogueRepository>();
        if ((await repository.GetProductsAsync(CancellationToken.None)).Count > 0) return;

        var report = await scope.ServiceProvider.GetRequiredService<WorkspaceCatalogueImporter>()
            .ImportAsync(new ImportInput(null), CancellationToken.None);
        app.Logger.LogInformation("Seeded catalogue from {Root}: {Created} products, {Images} images, {Warnings} warnings",
            report.SourceRoot, report.Created, report.ImagesStored, report.Warnings.Count);
    }
}
