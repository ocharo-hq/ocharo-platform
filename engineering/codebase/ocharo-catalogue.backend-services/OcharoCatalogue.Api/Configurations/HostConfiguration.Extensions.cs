using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
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
using OcharoCatalogue.Persistence.Import;
using Npgsql;
using WoW.Two.Sdk.Beta.Be.Data;
using WoW.Two.Sdk.Beta.Be.Data.Abstractions;
using WoW.Two.Sdk.Beta.Be.Data.EntityFrameworkCore;
using WoW.Two.Sdk.Beta.Be.Data.Errors;
using WoW.Two.Sdk.Beta.Be.Data.Migrations.Bespoke;
using WoW.Two.Sdk.Beta.Be.Files.Client;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Options;
using WoW.Two.Sdk.Beta.Be.Mediator;
using WoW.Two.Sdk.Beta.Be.Observability.HealthChecks;
using WoW.Two.Sdk.Beta.Be.Storage.Core;
using WoW.Two.Sdk.Beta.Be.Storage.FileSystem;
using WoW.Two.Sdk.Beta.Be.Web.Json;
using WoW.Two.Sdk.Beta.Be.Web.ProblemDetails;

namespace OcharoCatalogue.Api.Configurations;

public static partial class HostConfiguration
{
    /// <summary>Holds the root URL of the WoW2 Files instance Catalogue stores its product images and release artifacts
    /// in; unset, they stay in the local blob folder and the database.</summary>
    private const string FilesBaseUrlKey = "Files:BaseUrl";

    /// <summary>Registers Catalogue's PostgreSQL storage in the product database (<c>ConnectionStrings:Database</c>, filled
    /// per target by Wheelhouse): the <c>catalogue</c> migration set, the EF context that maps it, its readiness check on
    /// <c>/health</c>, and the import of the retired SQLite store with the gate that holds catalogue routes until it
    /// ran.</summary>
    /// <remarks>Every value resolves from the final configuration, so a test host's settings decide.</remarks>
    private static WebApplicationBuilder AddCatalogueDatabase(this WebApplicationBuilder builder)
    {
        // The configured string, not the data source's: a data source drops the password from its own copy.
        static string ConnectionString(IServiceProvider provider) =>
            provider.GetRequiredService<IConfiguration>().GetConnectionString(CatalogueDatabaseConstants.ConnectionStringName)
            ?? throw new InvalidOperationException($"ConnectionStrings:{CatalogueDatabaseConstants.ConnectionStringName} is required.");
        builder.Services.AddSingleton(provider => NpgsqlDataSource.Create(ConnectionString(provider)));
        builder.Services.AddKeyedSingleton<IDbConnectionFactory>(CatalogueDatabaseConstants.MigrationSetName, static (provider, _) =>
            new DataSourceConnectionFactory(provider.GetRequiredService<NpgsqlDataSource>()));
        builder.Services.AddKeyedSingleton(CatalogueDatabaseConstants.MigrationSetName, static (provider, _) =>
            new DatabaseSettings { ConnectionString = ConnectionString(provider) });
        builder.Services.AddDatabaseBespokeMigrationSet(
            CatalogueDatabaseConstants.MigrationSetName,
            static _ => new EmbeddedResourceMigrationBroker(typeof(OcharoCatalogueDbContext).Assembly, CatalogueDatabaseConstants.MigrationResourcePrefix),
            static migration =>
            {
                migration.SchemaName = CatalogueDatabaseConstants.Schema;
                migration.SchemaOwner = "product";
                migration.AdvisoryLockId = CatalogueDatabaseConstants.AdvisoryLockId;
                migration.Version = BuildIdentity.MigrationVersion;
                migration.Release = BuildIdentity.MigrationRelease;
            });
        builder.Services.AddDbContext<OcharoCatalogueDbContext>((provider, options) =>
            options.UseNpgsql(provider.GetRequiredService<NpgsqlDataSource>()));
        builder.Services.AddHealthChecks().AddDatabaseReadinessCheck<OcharoCatalogueDbContext>();

        builder.Services.AddSingleton<LegacySqliteSettings>(LegacySettings);
        builder.Services.AddSingleton<LegacySqliteImport>();
        return builder;
    }

    /// <summary>Resolves where the retired SQLite store and key ring sit: <c>LegacySqlite:{Path,KeysPath}</c>, else the
    /// paths the SQLite release used — <c>{Catalogue:DataRoot}/ocharo-catalogue.db</c> and <c>DataProtection:KeysPath</c>,
    /// else <c>{Catalogue:DataRoot}/keys</c> — relative to the content root.</summary>
    private static LegacySqliteSettings LegacySettings(IServiceProvider provider)
    {
        var configuration = provider.GetRequiredService<IConfiguration>();
        var contentRoot = provider.GetRequiredService<IHostEnvironment>().ContentRootPath;
        var dataRoot = provider.GetRequiredService<IOptions<CatalogueOptions>>().Value.DataRoot;
        string Rooted(string path) => Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(contentRoot, path));
        static string? Set(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
        var section = configuration.GetSection(LegacySqliteSettings.SectionName);
        return new LegacySqliteSettings
        {
            Path = Rooted(Set(section["Path"]) ?? Path.Combine(dataRoot, "ocharo-catalogue.db")),
            KeysPath = Rooted(Set(section["KeysPath"]) ?? Set(configuration["DataProtection:KeysPath"]) ?? Path.Combine(dataRoot, "keys")),
        };
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
    /// exception-to-result converter first. A save PostgreSQL could not serialize maps to the catalogue's busy conflict,
    /// and the be sdk's data rule maps a key or concurrency clash to a conflict.</summary>
    private static WebApplicationBuilder AddMediator(this WebApplicationBuilder builder)
    {
        builder.Services.AddMediator(typeof(InfrastructureAssembly).Assembly);
        builder.Services.AddExceptionMappingRule<PostgresBusyExceptionMappingRule>();
        builder.Services.AddDbExceptionMapping();
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
        builder.Services.AddControllersWithSdkJson();
        return builder;
    }

    /// <summary>Configures Catalogue's own HTTP boundary ahead of sign-in: response headers, the cross-origin write
    /// refusal, and the 503 every catalogue route answers while the retired SQLite store waits for its import.</summary>
    private static IApplicationBuilder UseCatalogueBoundary(this IApplicationBuilder app)
    {
        return app.UseMiddleware<CatalogueHttpBoundaryMiddleware>()
            .UseMiddleware<LegacyStoreGateMiddleware>();
    }

    /// <summary>Registers the media and release-artifact stores: WoW2 Files over HTTP when <see
    /// cref="FilesBaseUrlKey"/> is
    /// set, by tokens of Catalogue's own Identity client (a person's exchanged within their session, Catalogue's own
    /// otherwise), else the local blob folder and the database. Both read at resolve time, so a test host's settings decide
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

    /// <summary>Brings the database to this build's level — applied on a local run, verified on any other boot — then
    /// fills missing Files ids and seeds an empty catalogue from the configured records.</summary>
    /// <remarks>While the retired SQLite store waits for <c>import-sqlite</c>, neither runs: a seed or a backfill would
    /// write rows the copy then refuses as diverged.</remarks>
    private static async Task InitializeCatalogueAsync(this WebApplication app)
    {
        await app.Services.MigrateBespokeOnStartupAsync(app.Lifetime.ApplicationStopping);

        using var scope = app.Services.CreateScope();
        if (scope.ServiceProvider.GetRequiredService<LegacySqliteSettings>().IsWaiting)
        {
            app.Logger.LogWarning("The retired SQLite store waits for `import-sqlite`; catalogue routes answer 503 until it runs.");
            return;
        }

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
