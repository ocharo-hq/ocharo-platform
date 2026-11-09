using Npgsql;
using OcharoCatalogue.Persistence;

namespace OcharoCatalogue.Tests.Integration;

/// <summary>The <c>catalogue</c> set's scripts, run as embedded with the history bypassed: every Apply and Rollback is
/// idempotent and Rollback is a real inverse.</summary>
public sealed class CatalogueMigrationSetTests
{
    [Fact]
    public async Task Baseline_ShouldRerunAndInvert_WhenAppliedAndRolledBackTwice()
    {
        await using var dataSource = NpgsqlDataSource.Create(await CatalogueTestDatabase.CreateBareAsync());
        await RunAsync(dataSource, "CREATE SCHEMA catalogue");

        await RunAsync(dataSource, Script("Apply.sql"));
        await RunAsync(dataSource, Script("Apply.sql"));
        (await TablesAsync(dataSource)).Should().Be(12);
        await RunAsync(dataSource, Script("Rollback.sql"));
        await RunAsync(dataSource, Script("Rollback.sql"));
        (await TablesAsync(dataSource)).Should().Be(0);
        await RunAsync(dataSource, Script("Apply.sql"));
        (await TablesAsync(dataSource)).Should().Be(12);
    }

    private static string Script(string file)
    {
        var assembly = typeof(OcharoCatalogueDbContext).Assembly;
        using var stream = assembly.GetManifestResourceStream($"{CatalogueDatabaseConstants.MigrationResourcePrefix}001-baseline/{file}")!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Replace("{{schema}}", CatalogueDatabaseConstants.Schema, StringComparison.Ordinal);
    }

    private static async Task RunAsync(NpgsqlDataSource dataSource, string sql)
    {
        await using var command = dataSource.CreateCommand(sql);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<long> TablesAsync(NpgsqlDataSource dataSource)
    {
        await using var command = dataSource.CreateCommand("SELECT count(*) FROM information_schema.tables WHERE table_schema = 'catalogue'");
        return (long)(await command.ExecuteScalarAsync())!;
    }
}
