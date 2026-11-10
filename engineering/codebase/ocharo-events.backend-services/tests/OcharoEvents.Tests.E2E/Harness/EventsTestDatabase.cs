using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OcharoEvents.Persistence;
using Testcontainers.PostgreSql;
using WoW.Two.Sdk.Beta.Be.Caching.Postgres;
using WoW.Two.Sdk.Beta.Be.Data.Abstractions;
using WoW.Two.Sdk.Beta.Be.Data.EntityFrameworkCore;
using WoW.Two.Sdk.Beta.Be.Data.Migrations.Bespoke;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace OcharoEvents.Tests.Harness;

/// <summary>One PostgreSQL container per test run, and a fresh product database per test, copied from a template that
/// already holds the <c>ocharo_events</c> set and the session cache's, as the deploy's migrate step leaves them.</summary>
/// <remarks>A test host must claim the schemas as <see cref="OwnerInstance"/>, the instance the template applied them under.
/// Testcontainers' reaper removes the container when the test process ends.</remarks>
internal static class EventsTestDatabase
{
    /// <summary>The <c>Migrations:OwnerInstance</c> every test host and the template use.</summary>
    public const string OwnerInstance = "ocharo-events@test";

    private const string Template = "events_template";
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static PostgreSqlContainer? _container;

    /// <summary>Creates an empty product database from the template and returns its connection string.</summary>
    public static async Task<string> CreateAsync()
    {
        var admin = await EnsureTemplateAsync();
        var name = "events_" + Guid.NewGuid().ToString("N");
        await using (var connection = new NpgsqlConnection(admin))
        {
            await connection.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE {name} TEMPLATE {Template}", connection);
            await create.ExecuteNonQueryAsync();
        }

        return new NpgsqlConnectionStringBuilder(admin) { Database = name }.ConnectionString;
    }

    /// <summary>Registers the service's set and its session cache's as the host does, to apply them without a host.</summary>
    private static IServiceCollection AddEventsSets(this IServiceCollection services, string connectionString)
    {
        var dataSource = NpgsqlDataSource.Create(connectionString);
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [MigrationConstants.OwnerInstanceConfigurationKey] = OwnerInstance })
            .Build());
        services.AddSingleton(dataSource);
        services.AddKeyedSingleton<IDbConnectionFactory>(EventsDatabaseConstants.MigrationSetName, (_, _) => new DataSourceConnectionFactory(dataSource));
        services.AddKeyedSingleton(EventsDatabaseConstants.MigrationSetName, (_, _) => new DatabaseSettings { ConnectionString = connectionString });
        services.AddDatabaseBespokeMigrationSet(
            EventsDatabaseConstants.MigrationSetName,
            _ => new EmbeddedResourceMigrationBroker(typeof(OcharoEventsDbContext).Assembly, EventsDatabaseConstants.MigrationResourcePrefix),
            migration =>
            {
                migration.SchemaName = EventsDatabaseConstants.Schema;
                migration.SchemaOwner = "product";
                migration.AdvisoryLockId = EventsDatabaseConstants.AdvisoryLockId;
                migration.Release = "0.0.0";
            });
        services.AddPostgresDistributedCache(options =>
        {
            options.ConnectionString = connectionString;
            options.Schema = EventsDatabaseConstants.CacheSchema;
        });
        return services;
    }

    /// <summary>Brings a database to what a deployed host finds: the <c>ocharo_events</c> set and the session cache.</summary>
    private static async Task PrepareAsync(string connectionString)
    {
        await using var provider = new ServiceCollection().AddLogging().AddEventsSets(connectionString).BuildServiceProvider();
        var applied = await provider.ApplyMigrationSetsAsync("test");
        if (applied.IsFailure(out var error, out _))
            throw new InvalidOperationException($"The test database did not migrate: {error.Message}");
    }

    private static async Task<string> EnsureTemplateAsync()
    {
        await Gate.WaitAsync();
        try
        {
            if (_container is not null) return _container.GetConnectionString();

            var container = new PostgreSqlBuilder().WithImage("postgres:18-alpine").Build();
            await container.StartAsync();
            var admin = container.GetConnectionString();
            await using (var connection = new NpgsqlConnection(admin))
            {
                await connection.OpenAsync();
                await using var create = new NpgsqlCommand($"CREATE DATABASE {Template}", connection);
                await create.ExecuteNonQueryAsync();
            }

            await PrepareAsync(new NpgsqlConnectionStringBuilder(admin) { Database = Template, Pooling = false }.ConnectionString);
            NpgsqlConnection.ClearAllPools();
            _container = container;
            return admin;
        }
        finally
        {
            Gate.Release();
        }
    }
}
