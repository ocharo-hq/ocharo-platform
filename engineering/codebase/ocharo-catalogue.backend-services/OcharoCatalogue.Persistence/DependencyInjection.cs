using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OcharoCatalogue.Application.Catalogue.Repositories;
using OcharoCatalogue.Persistence.Catalogue;

namespace OcharoCatalogue.Persistence;

/// <summary>Registers the persistence layer — the EF Core SQLite context and the catalogue repository.</summary>
public static class DependencyInjection
{
    private const string DefaultConnection = "Data Source=App_Data/ocharo-catalogue.db";

    /// <summary>
    /// Adds <see cref="OcharoCatalogueDbContext"/> backed by SQLite (<c>ConnectionStrings:OcharoCatalogue</c>). The connection
    /// string is resolved lazily, so host/test overrides applied after registration still win; relative files anchor at
    /// the content root rather than the process working directory.
    /// </summary>
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<OcharoCatalogueDbContext>((provider, options) =>
        {
            var raw = (provider.GetService<IConfiguration>() ?? configuration)["ConnectionStrings:OcharoCatalogue"] ?? DefaultConnection;
            var builder = new SqliteConnectionStringBuilder(raw);
            var contentRoot = provider.GetService<IHostEnvironment>()?.ContentRootPath;
            if (builder.DataSource is { Length: > 0 } file && file != ":memory:" && !Path.IsPathRooted(file) && contentRoot is not null)
                builder.DataSource = Path.Combine(contentRoot, file);
            options.UseSqlite(builder.ToString());
        });
        services.AddScoped<ICatalogueRepository, CatalogueRepository>();
        return services;
    }

    /// <summary>Creates the SQLite schema on boot (document rows keep schema churn low; no migrations yet).</summary>
    public static async Task InitializeDatabaseAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OcharoCatalogueDbContext>();
        var source = new SqliteConnectionStringBuilder(db.Database.GetConnectionString()).DataSource;
        if (!string.IsNullOrEmpty(source) && source != ":memory:" && Path.GetDirectoryName(Path.GetFullPath(source)) is { } folder)
            Directory.CreateDirectory(folder);
        await db.Database.EnsureCreatedAsync();
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
    }
}
