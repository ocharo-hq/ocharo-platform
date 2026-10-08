using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OcharoCatalogue.Application;
using OcharoCatalogue.Application.Abstractions;
using OcharoCatalogue.Application.Catalogue.Models;
using OcharoCatalogue.Application.Catalogue.Repositories;
using OcharoCatalogue.Application.Catalogue.Services;
using OcharoCatalogue.Infrastructure.Catalogue;
using OcharoCatalogue.Infrastructure.Time;
using OcharoCatalogue.Persistence;
using OcharoCatalogue.Persistence.Catalogue;
using WoW.Two.Sdk.Backend.Beta.Mediator;
using WoW.Two.Sdk.Backend.Beta.Storage.Core;
using WoW.Two.Sdk.Backend.Beta.Storage.FileSystem;
using WoW.Two.Sdk.Backend.Beta.Web.ProblemDetails;

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
        builder.Services.AddScoped<IMediaStore, MediaStore>();
        builder.Services.AddScoped<WorkspaceCatalogueImporter>();
        builder.Services.AddSingleton<IDesignArtifactValidator, DesignArtifactValidator>();
        builder.Services.AddScoped<SettingsService>();
        builder.Services.AddScoped<ProductService>();
        builder.Services.AddScoped<CollectionService>();
        builder.Services.AddScoped<PublicCatalogueService>();
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

    /// <summary>Registers the HTTP error boundary, controllers and OpenAPI.</summary>
    private static WebApplicationBuilder AddControllers(this WebApplicationBuilder builder)
    {
        builder.Services.AddHttpProblemDetails();
        builder.Services.AddControllers();
        builder.Services.AddOpenApi();
        return builder;
    }

    /// <summary>Configures catalogue storage and imports configured records into an empty catalogue.</summary>
    private static async Task InitializeCatalogueAsync(this WebApplication app)
    {
        await app.Services.InitializeDatabaseAsync();

        using var scope = app.Services.CreateScope();
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
