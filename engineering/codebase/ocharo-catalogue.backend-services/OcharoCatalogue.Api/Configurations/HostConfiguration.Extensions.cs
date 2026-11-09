using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OcharoCatalogue.Api.Catalogue.Core.Mappers;
using OcharoCatalogue.Api.Catalogue.Core.Middlewares;
using OcharoCatalogue.Api.Catalogue.Core.Services;
using OcharoCatalogue.Api.RuntimeConfig.Settings;
using OcharoCatalogue.Application.Catalogue.Core.Repositories;
using OcharoCatalogue.Application.Catalogue.Core.Services;
using OcharoCatalogue.Application.Catalogue.Importing.Models;
using OcharoCatalogue.Application.Catalogue.Media.Services;
using OcharoCatalogue.Application.Catalogue.Publishing.Services;
using OcharoCatalogue.Infrastructure;
using OcharoCatalogue.Infrastructure.Catalogue.Collections.FoundationServices;
using OcharoCatalogue.Infrastructure.Catalogue.Core.Options;
using OcharoCatalogue.Infrastructure.Catalogue.Designs.FoundationServices;
using OcharoCatalogue.Infrastructure.Catalogue.Importing.FoundationServices;
using OcharoCatalogue.Infrastructure.Catalogue.Media.FoundationServices;
using OcharoCatalogue.Infrastructure.Catalogue.Media.Stores;
using OcharoCatalogue.Infrastructure.Catalogue.Products.FoundationServices;
using OcharoCatalogue.Infrastructure.Catalogue.Publishing.FoundationServices;
using OcharoCatalogue.Infrastructure.Catalogue.Publishing.Stores;
using OcharoCatalogue.Infrastructure.Catalogue.Publishing.Validators;
using OcharoCatalogue.Infrastructure.Catalogue.Settings.FoundationServices;
using OcharoCatalogue.Persistence;
using OcharoCatalogue.Persistence.Catalogue;
using WoW.Two.Sdk.Beta.Be.Files.Client;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Options;
using WoW.Two.Sdk.Beta.Be.Mediator;
using WoW.Two.Sdk.Beta.Be.Observability.HealthChecks;
using WoW.Two.Sdk.Beta.Be.Storage.Core;
using WoW.Two.Sdk.Beta.Be.Storage.FileSystem;
using WoW.Two.Sdk.Beta.Be.Web.ErrorMapping;
using WoW.Two.Sdk.Beta.Be.Web.Json;
using WoW.Two.Sdk.Beta.Be.Web.ProblemDetails;

namespace OcharoCatalogue.Api.Configurations;

public static partial class HostConfiguration
{
    /// <summary>Holds the root URL of the WoW2 Files instance Catalogue stores its product images and release artifacts
    /// in;
    /// unset, they stay in the local blob folder and SQLite.</summary>
    private const string FilesBaseUrlKey = "Files:BaseUrl";

    /// <summary>Registers the SQLite catalogue context with lazy host configuration, and its readiness check on
    /// <c>/health</c>.</summary>
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
        builder.Services.AddHealthChecks().AddDatabaseReadinessCheck<OcharoCatalogueDbContext>();
        return builder;
    }

    /// <summary>Registers catalogue settings, repositories, media, import and the foundation services the handlers
    /// share.</summary>
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
        builder.Services.AddSingleton<IDesignArtifactValidator, DesignArtifactValidator>();
        builder.Services.AddScoped<ICatalogueCaller, HttpCatalogueCaller>();
        builder.Services.AddScoped<CatalogueSettingsService>();
        builder.Services.AddScoped<ProductDetailService>();
        builder.Services.AddScoped<CollectionSaveService>();
        builder.Services.AddScoped<DesignSaveService>();
        builder.Services.AddScoped<MediaAccessService>();
        builder.Services.AddScoped<MediaVerificationService>();
        builder.Services.AddScoped<ReleasePreparationService>();
        builder.Services.AddScoped<WorkspaceImportService>();
        return builder;
    }

    /// <summary>Registers the mediator over the handler-bearing Infrastructure assembly; the SDK installs its
    /// exception-to-result converter first, and a busy SQLite store maps to the catalogue's busy conflict.</summary>
    private static WebApplicationBuilder AddMediator(this WebApplicationBuilder builder)
    {
        builder.Services.AddMediator(typeof(InfrastructureAssembly).Assembly);
        builder.Services.AddExceptionMappingRule<SqliteBusyExceptionMappingRule>();
        return builder;
    }

    /// <summary>
    /// Registers the public runtime settings the SPA reads at startup: the Ocharo app and site addresses.
    /// </summary>
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

    /// <summary>Registers the HTTP error boundary (problem details for every client, <c>no-store</c> errors), the
    /// catalogue's error statuses and controllers on the SDK wire contract, which keeps dictionary keys such as size
    /// labels as written.</summary>
    private static WebApplicationBuilder AddControllers(this WebApplicationBuilder builder)
    {
        builder.Services.AddHttpProblemDetails();
        builder.Services.AddSingleton<IErrorHttpStatusCodeMapper, CatalogueErrorHttpStatusCodeMapper>();
        builder.Services.AddControllersWithSdkJson();
        return builder;
    }

    /// <summary>Configures Catalogue's own HTTP boundary ahead of sign-in: response headers and the cross-origin write
    /// refusal.</summary>
    private static IApplicationBuilder UseCatalogueBoundary(this IApplicationBuilder app)
    {
        return app.UseMiddleware<CatalogueHttpBoundaryMiddleware>();
    }

    /// <summary>Registers the media and release-artifact stores: WoW2 Files over HTTP when <see
    /// cref="FilesBaseUrlKey"/> is
    /// set, by tokens of Catalogue's own Identity client (a person's exchanged within their session, Catalogue's own
    /// otherwise), else the local blob folder and SQLite. Both read at resolve time, so a test host's settings decide
    /// (research/files-adoption.md #24, #26).</summary>
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
        builder.Services.AddScoped<FilesMediaBackfillService>();
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

    /// <summary>
    /// Fills the file ids the migration made; an unreachable Files leaves those files on local bytes, never the boot.
    /// </summary>
    private static async Task BackfillFileIdsAsync(this IServiceProvider services, ILogger logger)
    {
        try
        {
            await services.GetRequiredService<FilesMediaBackfillService>().RunAsync(CancellationToken.None);
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

        var imported = await scope.ServiceProvider.GetRequiredService<WorkspaceImportService>()
            .ImportAsync(new ImportInput(null), CancellationToken.None);
        imported.Match(
            success =>
            {
                var report = success.Data;
                app.Logger.LogInformation("Seeded catalogue from {Root}: {Created} products, {Images} images, {Warnings} warnings",
                    report.SourceRoot, report.Created, report.ImagesStored, report.Warnings.Count);
                return true;
            },
            failure =>
            {
                app.Logger.LogWarning("Catalogue was not seeded: {Message}", failure.Error.Message);
                return false;
            });
    }
}
