using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OcharoBrand.Application.Brand;
using OcharoBrand.Persistence.Brand;

namespace OcharoBrand.Persistence;

/// <summary>Registers the persistence layer — the EF Core SQLite context and the brand repository.</summary>
public static class DependencyInjection
{
    private const string DefaultConnection = "Data Source=App_Data/ocharo-brand.db";

    /// <summary>
    /// Adds <see cref="OcharoBrandDbContext"/> backed by SQLite (<c>ConnectionStrings:OcharoBrand</c>). The connection
    /// string is resolved lazily, so host/test overrides applied after registration still win; relative files anchor at
    /// the content root rather than the process working directory.
    /// </summary>
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<OcharoBrandDbContext>((provider, options) =>
        {
            var raw = (provider.GetService<IConfiguration>() ?? configuration)["ConnectionStrings:OcharoBrand"] ?? DefaultConnection;
            var builder = new SqliteConnectionStringBuilder(raw);
            var contentRoot = provider.GetService<IHostEnvironment>()?.ContentRootPath;
            if (builder.DataSource is { Length: > 0 } file && file != ":memory:" && !Path.IsPathRooted(file) && contentRoot is not null)
                builder.DataSource = Path.Combine(contentRoot, file);
            options.UseSqlite(builder.ToString());
        });
        services.AddScoped<IBrandRepository, BrandRepository>();
        return services;
    }

    /// <summary>Creates the SQLite schema on boot (document rows keep schema churn low; no migrations yet).</summary>
    public static async Task InitializeDatabaseAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OcharoBrandDbContext>();
        var source = new SqliteConnectionStringBuilder(db.Database.GetConnectionString()).DataSource;
        if (!string.IsNullOrEmpty(source) && source != ":memory:" && Path.GetDirectoryName(Path.GetFullPath(source)) is { } folder)
            Directory.CreateDirectory(folder);
        await db.Database.EnsureCreatedAsync();
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
    }
}
