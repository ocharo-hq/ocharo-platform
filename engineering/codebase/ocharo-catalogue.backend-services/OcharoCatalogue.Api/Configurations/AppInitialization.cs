using Microsoft.Extensions.Options;
using OcharoCatalogue.Application.Catalogue.Models;
using OcharoCatalogue.Application.Catalogue.Repositories;
using OcharoCatalogue.Infrastructure.Catalogue;
using OcharoCatalogue.Persistence;

namespace OcharoCatalogue.Api.Configurations;

/// <summary>Startup initialization — creates the schema and seeds an empty catalogue from the workspace records.</summary>
public static class AppInitialization
{
    /// <summary>Creates the SQLite schema, then imports the configured source folder when no product exists yet.</summary>
    public static async Task InitializeAsync(this WebApplication app)
    {
        await app.Services.InitializeDatabaseAsync();

        using var scope = app.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<CatalogueOptions>>().Value;
        if (!options.SeedOnEmpty || string.IsNullOrWhiteSpace(options.SourceRoot) || !Directory.Exists(options.SourceRoot)) return;
        var repository = scope.ServiceProvider.GetRequiredService<ICatalogueRepository>();
        if ((await repository.GetProductsAsync(CancellationToken.None)).Count > 0) return;

        var report = await scope.ServiceProvider.GetRequiredService<WorkspaceCatalogueImporter>()
            .ImportAsync(new ImportInput(null), CancellationToken.None);
        app.Logger.LogInformation("Seeded catalogue from {Root}: {Created} products, {Images} images, {Warnings} warnings",
            report.SourceRoot, report.Created, report.ImagesStored, report.Warnings.Count);
    }
}
