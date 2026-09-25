using System.Data.Common;
using BrandWorkspace.Application.Foundation.Repositories;
using BrandWorkspace.Domain.Foundation.Models;
using BrandWorkspace.Persistence.Foundation.Repositories;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Npgsql;
using WoW.Two.Sdk.Backend.Beta.Data.Abstractions;
using WoW.Two.Sdk.Backend.Beta.Data.Migrations.Bespoke;
using WoW.Two.Sdk.Backend.Beta.Foundation.Results;

namespace BrandWorkspace.Api.Configurations;

public static partial class HostConfiguration
{
    /// <summary>Registers the permitted deployment environment inputs.</summary>
    private static WebApplicationBuilder AddExplicitEnvironment(this WebApplicationBuilder builder)
    {
        foreach (var source in builder.Configuration.Sources.OfType<EnvironmentVariablesConfigurationSource>().ToArray())
            builder.Configuration.Sources.Remove(source);
        var values = new Dictionary<string, string?>();
        if (Environment.GetEnvironmentVariable("ConnectionStrings__Database") is { } connection)
            values["ConnectionStrings:Database"] = connection;
        if (Environment.GetEnvironmentVariable("ASPNETCORE_URLS") is { } urls) values["urls"] = urls;
        if (Environment.GetEnvironmentVariable("AllowedHosts") is { } allowedHosts) values["AllowedHosts"] = allowedHosts;
        builder.Configuration.AddInMemoryCollection(values);
        return builder;
    }

    /// <summary>Registers the PostgreSQL connection pool and SDK connection factory.</summary>
    private static WebApplicationBuilder AddPostgresDatabase(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton(services => NpgsqlDataSource.Create(
            services.GetRequiredService<IConfiguration>().GetConnectionString("Database")
            ?? throw new InvalidOperationException("ConnectionStrings:Database is required.")));
        builder.Services.AddSingleton<DbDataSource>(services => services.GetRequiredService<NpgsqlDataSource>());
        builder.Services.AddDataSourceConnectionFactory();
        builder.Services.AddSingleton<IFoundationRepository, FoundationRepository>();
        builder.Services.AddSingleton(TimeProvider.System);
        return builder;
    }

    /// <summary>Registers migration services exclusively for the explicit CLI invocation.</summary>
    private static WebApplicationBuilder AddFoundation(this WebApplicationBuilder builder, bool migrate)
    {
        if (!migrate) return builder;
        builder.Services.AddDatabaseBespokeMigrations(typeof(FoundationRepository).Assembly, options =>
        {
            options.Version = "r0.1";
            options.AdvisoryLockId = 8_264_001;
        });
        builder.Services.AddHangfire(configuration => configuration.UsePostgreSqlStorage(
            storage => storage.UseNpgsqlConnection(builder.Configuration.GetConnectionString("Database")),
            new PostgreSqlStorageOptions { PrepareSchemaIfNecessary = true }));
        return builder;
    }

    /// <summary>Registers JSON errors for the HTTP surface.</summary>
    private static WebApplicationBuilder AddHttpSurface(this WebApplicationBuilder builder)
    {
        builder.Services.AddProblemDetails();
        return builder;
    }

    /// <summary>Configures public technical status and SPA endpoints without business mutations.</summary>
    private static WebApplication AddFoundationEndpoints(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.MapGet("/health/live", () => Results.Ok(new { status = "alive" }));
        app.MapGet("/health/ready", async (IFoundationRepository repository, CancellationToken token) =>
        {
            try
            {
                return await repository.IsReadyAsync(token) ? Results.Ok(new { status = "ready" })
                    : Results.Problem(statusCode: 503, title: "Database schema is not ready");
            }
            catch (Exception exception) when (exception is NpgsqlException or TimeoutException)
            {
                return Results.Problem(statusCode: 503, title: "Database schema is not ready");
            }
        });
        app.MapGet("/api/system/status", async (IFoundationRepository repository, TimeProvider time, CancellationToken token) =>
        {
            try
            {
                if (!await repository.IsReadyAsync(token))
                    return Results.Problem(statusCode: 503, title: "Database schema is not ready");
                var worker = WorkerSnapshot.From(await repository.GetLastHeartbeatAsync(token), time.GetUtcNow());
                return Results.Ok(new { service = "brand-workspace", stage = "foundation", persistence = "PostgreSQL", worker });
            }
            catch (Exception exception) when (exception is NpgsqlException or TimeoutException)
            {
                return Results.Problem(statusCode: 503, title: "Database schema is not ready");
            }
        });
        app.MapFallback("/api/{**path}", () => Results.Problem(statusCode: 404, title: "Not Found"));
        app.MapFallbackToFile("index.html");
        return app;
    }

    /// <summary>Runs the explicit migration command or starts the API; neither path starts a worker.</summary>
    public static async Task RunCommandOrHostAsync(this WebApplication app, string[] args)
    {
        if (args.Length == 1 && args[0] == "--migrate")
        {
            var result = await app.Services.GetRequiredService<IMigrationRunnerService>().ApplyPendingAsync("api-cli");
            if (result is not Result<IReadOnlyList<string>>.Success success)
                throw new InvalidOperationException("Migration failed; inspect schema history and source integrity.");
            // Resolving the storage initializes its provider-owned schema only in this explicit CLI.
            _ = app.Services.GetRequiredService<JobStorage>();
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { migrated = success.Value, queueSchema = "ready" }));
            await app.DisposeAsync();
            return;
        }
        if (args.Contains("--migrate")) throw new ArgumentException("Use --migrate alone.");
        await app.RunAsync();
    }
}
