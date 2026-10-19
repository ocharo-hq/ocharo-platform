using Ocharo.Sso;
using WoW.Two.Sdk.Beta.Be.Web.ProblemDetails;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OcharoBrand.Api.Brand;
using OcharoBrand.Application;
using OcharoBrand.Application.Abstractions;
using OcharoBrand.Application.Brand;
using OcharoBrand.Infrastructure.Brand;
using OcharoBrand.Infrastructure.Time;
using OcharoBrand.Persistence;
using OcharoBrand.Persistence.Brand;
using WoW.Two.Sdk.Beta.Be.Foundation.Configuration;
using WoW.Two.Sdk.Beta.Be.Mediator;
using WoW.Two.Sdk.Beta.Be.Storage.Core;
using WoW.Two.Sdk.Beta.Be.Storage.FileSystem;

namespace OcharoBrand.Api.Configurations;

public static partial class HostConfiguration
{
    /// <summary>Registers the SQLite context with final host configuration and content-root-relative storage.</summary>
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
        return builder;
    }

    /// <summary>Registers brand identity, media, draft/release workflows and their persistence adapters.</summary>
    private static WebApplicationBuilder AddBrand(this WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<IBrandRepository, BrandRepository>();
        builder.Services.AddSingleton<IClock, SystemClock>();
        builder.Services.AddOptions<BrandOptions>().Bind(builder.Configuration.GetSection(BrandOptions.Section));
        // Resolve final options after host/test overrides; retain the host content-root anchor.
        var contentRoot = builder.Environment.ContentRootPath;
        builder.Services.AddSingleton<IBlobRepository>(provider =>
        {
            var dataRoot = provider.GetRequiredService<IOptions<BrandOptions>>().Value.DataRoot;
            var root = Path.IsPathRooted(dataRoot) ? dataRoot : Path.Combine(contentRoot, dataRoot);
            return new LocalFileBlobRepository(Path.Combine(root, "blobs"));
        });
        builder.Services.AddScoped<IMediaStore, MediaStore>();
        builder.Services.AddScoped<BrandSeeder>();
        builder.Services.AddMediator(typeof(ApplicationAssemblyMarker).Assembly);
        builder.Services.AddScoped<BrandService>();
        builder.Services.AddScoped<PublicBrandService>();
        return builder;
    }

    /// <summary>Registers controller delivery, OpenAPI and JSON errors for the HTTP surface.</summary>
    private static WebApplicationBuilder AddHttpSurface(this WebApplicationBuilder builder)
    {
        builder.Services.AddHttpProblemDetails();
        builder.Services.AddControllers();
        builder.Services.AddOpenApi();
        return builder;
    }

    /// <summary>Configures the existing brand API middleware, technical endpoints and operator SPA.</summary>
    private static WebApplication ConfigureHttpSurface(this WebApplication app)
    {
        app.UseHttpProblemDetails();

        app.UseMiddleware<BrandExceptionMiddleware>();
        app.UseOcharoSso(IsPublicSsoRequest);
        app.UseMiddleware<LocalPilotBoundaryMiddleware>();
        app.Use(RequirePublicationApproval);
        if (app.Environment.IsLocalRun(app.Configuration))
            app.MapOpenApi();
        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.MapControllers();
        app.MapGet("/api/system/version", (HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            return Results.Ok(new { service = "OcharoBrand", version = BuildIdentity.Version });
        }).AllowAnonymous();
        app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "OcharoBrand", mode = "local-pilot" }));
        app.MapFallback("/api/{**slug}", () => Results.Problem(statusCode: 404, title: "API route not found"));
        app.MapFallbackToFile("index.html");
        return app;
    }

    /// <summary>Configures mandatory brand database preparation and seeds an empty brand before traffic starts.</summary>
    private static async Task<WebApplication> InitializeBrandAsync(this WebApplication app)
    {
        await app.Services.InitializeBrandDatabaseAsync();
        using var scope = app.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<BrandSeeder>().SeedIfEmptyAsync(CancellationToken.None);
        return app;
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
    }
}
