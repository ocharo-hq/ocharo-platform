using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OcharoCatalogue.Persistence.Catalogue;

namespace OcharoCatalogue.Persistence;

/// <summary>Initializes the catalogue SQLite schema.</summary>
public static class CatalogueDatabaseInitialization
{
    /// <summary>Creates the SQLite schema on boot (document rows keep schema churn low; no migrations yet).</summary>
    public static async Task InitializeDatabaseAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OcharoCatalogueDbContext>();
        var source = new SqliteConnectionStringBuilder(db.Database.GetConnectionString()).DataSource;
        if (!string.IsNullOrEmpty(source) && source != ":memory:" && Path.GetDirectoryName(Path.GetFullPath(source)) is { } folder)
            Directory.CreateDirectory(folder);
        await db.Database.EnsureCreatedAsync();
        await CatalogueDesignSchema.EnsureAsync(db);
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
    }
}
