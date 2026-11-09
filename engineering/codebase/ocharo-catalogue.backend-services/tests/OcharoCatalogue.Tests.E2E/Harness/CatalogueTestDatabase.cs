using System.Collections.Concurrent;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OcharoCatalogue.Api.Configurations;
using Testcontainers.PostgreSql;
using WoW.Two.Sdk.Beta.Be.Data.Migrations.Bespoke;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace OcharoCatalogue.Tests.Harness;

/// <summary>One PostgreSQL 18 container per test run, and a fresh product database per call, copied from a template that
/// already holds what the deploy's <c>migrate</c> step leaves: Catalogue's <c>catalogue</c> set and its session cache's
/// <c>distributed_cache</c> set in <c>catalogue_cache</c>, applied by the host's own registrations.</summary>
/// <remarks>A test host must claim the schemas as <see cref="OwnerInstance"/>, the instance the template applied them
/// under: <see cref="Settings"/> carries both keys. Testcontainers' reaper removes the container when the run ends.</remarks>
public static class CatalogueTestDatabase
{
    /// <summary>The <c>Migrations:OwnerInstance</c> every test host and the template use.</summary>
    public const string OwnerInstance = "ocharo-catalogue@test";

    /// <summary>The key of the product database's connection string.</summary>
    public const string ConnectionKey = "ConnectionStrings:Database";

    /// <summary>The key of the owner instance a host claims its schemas as.</summary>
    public const string OwnerKey = MigrationConstants.OwnerInstanceConfigurationKey;

    private const string Template = "catalogue_template";
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly ConcurrentDictionary<string, Lazy<string>> Named = new(StringComparer.Ordinal);
    private static PostgreSqlContainer? container;

    /// <summary>The database a test's store <paramref name="name"/> keeps across every host it boots: created from the
    /// template on first use, the same one after, as a restarted host finds its store.</summary>
    public static string For(string name) =>
        Named.GetOrAdd(name, _ => new Lazy<string>(() => Task.Run(CreateAsync).GetAwaiter().GetResult())).Value;

    /// <summary>Creates an empty, migrated product database from the template and returns its connection string.</summary>
    public static async Task<string> CreateAsync()
    {
        var admin = await EnsureTemplateAsync();
        var name = "catalogue_" + Guid.NewGuid().ToString("N");
        await using (var connection = new NpgsqlConnection(admin))
        {
            await connection.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE {name} TEMPLATE {Template}", connection);
            await create.ExecuteNonQueryAsync();
        }

        return new NpgsqlConnectionStringBuilder(admin) { Database = name }.ConnectionString;
    }

    /// <summary>Creates a database the server holds nothing in, not even a schema: what a target's first deploy finds.</summary>
    public static async Task<string> CreateBareAsync()
    {
        var admin = await EnsureTemplateAsync();
        var name = "catalogue_bare_" + Guid.NewGuid().ToString("N");
        await using (var connection = new NpgsqlConnection(admin))
        {
            await connection.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE {name}", connection);
            await create.ExecuteNonQueryAsync();
        }

        return new NpgsqlConnectionStringBuilder(admin) { Database = name }.ConnectionString;
    }

    /// <summary>A host's database settings: <paramref name="connectionString"/> and the owner instance the template applied
    /// its sets under.</summary>
    public static IReadOnlyDictionary<string, string?> Settings(string connectionString) => new Dictionary<string, string?>
    {
        [ConnectionKey] = connectionString,
        [MigrationConstants.OwnerInstanceConfigurationKey] = OwnerInstance,
    };

    /// <summary>Applies every set the host registers to <paramref name="connectionString"/>, as the deploy's
    /// <c>migrate</c> step does; idempotent.</summary>
    public static async Task MigrateAsync(string connectionString)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(HostConfiguration).Assembly.GetName().Name,
            ContentRootPath = AppContext.BaseDirectory,
            EnvironmentName = "Testing",
        });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(Settings(connectionString));
        builder.Configure();
        await using var provider = builder.Services.BuildServiceProvider();
        var applied = await provider.ApplyMigrationSetsAsync("test");
        if (applied.IsFailure(out var error, out _))
            throw new InvalidOperationException($"The test database did not migrate: {error.Message}");
    }

    private static async Task<string> EnsureTemplateAsync()
    {
        await Gate.WaitAsync();
        try
        {
            if (container is not null) return container.GetConnectionString();

            var started = new PostgreSqlBuilder().WithImage("postgres:18-alpine").Build();
            await started.StartAsync();
            var admin = started.GetConnectionString();
            await using (var connection = new NpgsqlConnection(admin))
            {
                await connection.OpenAsync();
                await using var create = new NpgsqlCommand($"CREATE DATABASE {Template}", connection);
                await create.ExecuteNonQueryAsync();
            }

            await MigrateAsync(new NpgsqlConnectionStringBuilder(admin) { Database = Template, Pooling = false }.ConnectionString);
            NpgsqlConnection.ClearAllPools();
            container = started;
            return admin;
        }
        finally
        {
            Gate.Release();
        }
    }
}
