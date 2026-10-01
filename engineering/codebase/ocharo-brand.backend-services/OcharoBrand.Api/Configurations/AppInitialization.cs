using OcharoBrand.Infrastructure.Brand;
using OcharoBrand.Persistence;

namespace OcharoBrand.Api.Configurations;

/// <summary>Startup initialization — creates the schema and seeds an empty brand (release 1).</summary>
public static class AppInitialization
{
    public static async Task InitializeAsync(this WebApplication app)
    {
        await app.Services.InitializeDatabaseAsync();
        using var scope = app.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<BrandSeeder>().SeedIfEmptyAsync(CancellationToken.None);
    }
}
