using System.Net;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using OcharoCatalogue.Api.Catalogue.Core.Verbs;

namespace OcharoCatalogue.Tests.E2E;

/// <summary>While the retired SQLite store waits on the volume, every catalogue route answers 503; <c>import-sqlite</c>
/// copies it over the host's own services, and the published <c>wave1-check</c> edition and its PDF then answer from
/// PostgreSQL.</summary>
public sealed class LegacySqliteImportVerbTests : IAsyncLifetime
{
    private readonly string root = Directory.CreateTempSubdirectory("ocharo-catalogue-legacy-").FullName;

    private string StorePath => Path.Combine(root, "ocharo-catalogue.db");

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(root, recursive: true);
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Routes_ShouldAnswer503UntilTheImport_ThenServeThePublishedEditionAndItsPdf()
    {
        Guid releaseId;
        byte[] pdf;
        await using (var source = await CatalogueHarness.CreateAsync())
        {
            (_, releaseId, pdf) = await PublishedEditionFixture.PublishAsync(source);
            await LegacyCatalogueStore.WriteAsync(source.Get<IConfiguration>()[CatalogueTestDatabase.ConnectionKey]!, StorePath);
        }

        await using var host = CatalogueHost.Create(new Dictionary<string, string?>
        {
            ["LegacySqlite:Path"] = StorePath,
            ["LegacySqlite:KeysPath"] = Path.Combine(root, "keys"),
        });
        using var anonymous = CatalogueSignIn.CreateClient(host.Factory, person: null);
        using var owner = CatalogueSignIn.CreateClient(host.Factory, CatalogueSignIn.Owner);
        const string edition = $"/api/public/catalogue/editions/{PublishedEditionFixture.Slug}?locale={PublishedEditionFixture.Locale}";

        (await anonymous.GetAsync(edition)).StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await owner.GetAsync("/api/catalogue/products")).StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await anonymous.GetAsync("/health")).StatusCode.Should().Be(HttpStatusCode.OK);

        var (planned, plan) = await RunAsync(host, "--dry-run");
        planned.Should().Be(0);
        plan.GetProperty("status").GetString().Should().Be("planned");
        (await anonymous.GetAsync(edition)).StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);

        var (exit, result) = await RunAsync(host);
        exit.Should().Be(0);
        result.GetProperty("status").GetString().Should().Be("imported");
        result.GetProperty("publications").GetInt32().Should().Be(1);
        result.GetProperty("tables").EnumerateArray()
            .Should().AllSatisfy(table => table.GetProperty("postgresSha256").GetString().Should().Be(table.GetProperty("sqliteSha256").GetString()));

        var read = await anonymous.GetAsync(edition);
        read.StatusCode.Should().Be(HttpStatusCode.OK);
        var asset = await anonymous.GetAsync(
            $"/api/public/catalogue/editions/{PublishedEditionFixture.Slug}/assets/catalogue.pdf?locale={PublishedEditionFixture.Locale}&release={releaseId}");
        asset.StatusCode.Should().Be(HttpStatusCode.OK);
        (await asset.Content.ReadAsByteArrayAsync()).Should().Equal(pdf);
        (await owner.GetStringAsync("/api/catalogue/products")).Should().Contain(PublishedEditionFixture.ProductId);

        var (rerun, current) = await RunAsync(host);
        rerun.Should().Be(0);
        current.GetProperty("status").GetString().Should().Be("current");
    }

    [Fact]
    public async Task Verb_ShouldRefuseAnUnknownArgument()
    {
        await using var host = CatalogueHost.Create(new Dictionary<string, string?> { ["LegacySqlite:Path"] = StorePath });

        var (exit, result) = await RunAsync(host, "--force");

        exit.Should().Be(1);
        result.GetProperty("status").GetString().Should().Be("refused");
    }

    /// <summary>Runs the verb over the host's services; returns its exit and its last stdout line's JSON.</summary>
    private static async Task<(int Exit, JsonElement Result)> RunAsync(CatalogueHost host, params string[] arguments)
    {
        using var output = new StringWriter();
        var exit = await LegacySqliteImportVerb.RunAsync(host.Factory.Services, arguments, output, TextWriter.Null, CancellationToken.None);
        var last = output.ToString().TrimEnd().Split('\n')[^1];
        last.Should().StartWith("import-result ");
        return (exit, JsonDocument.Parse(last["import-result ".Length..]).RootElement.Clone());
    }
}
