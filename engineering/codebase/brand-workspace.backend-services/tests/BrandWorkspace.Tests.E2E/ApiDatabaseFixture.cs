using Testcontainers.PostgreSql;
using WoW.Two.Sdk.Beta.Be.Testing.Containers.Postgres;

namespace BrandWorkspace.Tests.E2E;

/// <summary>One PostgreSQL database for contract tests; the first API host to start applies its schema.</summary>
public sealed class ApiDatabaseFixture : IAsyncLifetime
{
    private readonly PostgresFixture _database = new(new PostgreSqlBuilder().WithImage("postgres:18-alpine").Build());
    public string Connection => _database.ConnectionString;
    public async Task InitializeAsync() => await _database.StartAsync();
    public async Task DisposeAsync() => await _database.DisposeAsync();
}
