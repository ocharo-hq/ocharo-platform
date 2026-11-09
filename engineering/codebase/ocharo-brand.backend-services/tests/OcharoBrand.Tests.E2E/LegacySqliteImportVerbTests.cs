using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using OcharoBrand.Api.Brand.Import;
using OcharoBrand.Tests.Support;

namespace OcharoBrand.Tests.E2E;

/// <summary>A Brand deployed beside its retired SQLite store answers every brand route 503 and seeds nothing until
/// <c>import-sqlite</c> runs over the host's own services; then it serves release 1 and the draft exactly as the SQLite
/// release did, every logo still on its WoW2 Files file id.</summary>
public sealed class LegacySqliteImportVerbTests : IAsyncDisposable
{
    private readonly FilesWorkspace source = new();
    private readonly FilesWorkspace target = new();
    private readonly TestFiles files = new();

    public async ValueTask DisposeAsync()
    {
        await source.DisposeAsync();
        SqliteConnection.ClearAllPools();
        await target.DisposeAsync();
    }

    [Fact]
    public async Task BrandRoutes_ShouldAnswer503_UntilTheImportCopiesTheStore_ThenServeReleaseOne()
    {
        var seeded = source.Host(files);
        JsonElement kitBefore, draftBefore;
        using (var reader = await BrandTestIdentity.SignedIn(seeded, BrandTestIdentity.Owner))
        {
            kitBefore = await reader.GetFromJsonAsync<JsonElement>("/api/public/brand");
            draftBefore = (await reader.GetFromJsonAsync<JsonElement>("/api/brand/draft")).GetProperty("data");
        }
        await source.StopAsync(seeded);
        var legacy = Path.Combine(target.Root, "volume", "ocharo-brand.db");
        await LegacyBrandStore.CopyFromPostgresAsync(source.Database, legacy);
        var moving = target.Host(files, new()
        {
            ["LegacySqlite:Path"] = legacy,
            ["LegacySqlite:KeysPath"] = Path.Combine(target.Root, "volume", "keys"),
        });
        using var anonymous = BrandTestIdentity.Anonymous(moving);
        using var owner = await BrandTestIdentity.SignedIn(moving, BrandTestIdentity.Owner);

        var kitMoving = await anonymous.GetAsync("/api/public/brand");
        var draftMoving = await owner.GetAsync("/api/brand/draft");
        var (planExit, plan) = await RunAsync(moving, "--dry-run");
        var kitPlanned = await anonymous.GetAsync("/api/public/brand");
        var (exit, result) = await RunAsync(moving);
        var kitAfter = await anonymous.GetFromJsonAsync<JsonElement>("/api/public/brand");
        var pinned = await anonymous.GetAsync("/api/public/brand/releases/1");
        var draftAfter = (await owner.GetFromJsonAsync<JsonElement>("/api/brand/draft")).GetProperty("data");
        var (rerunExit, rerun) = await RunAsync(moving);

        kitMoving.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await kitMoving.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("brandCode").GetString().Should().Be("storage_moving");
        draftMoving.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        planExit.Should().Be(0);
        plan.GetProperty("status").GetString().Should().Be("planned");
        kitPlanned.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        exit.Should().Be(0);
        result.GetProperty("status").GetString().Should().Be("imported");
        result.GetProperty("diverged").GetInt32().Should().Be(0);
        result.GetProperty("releases").GetInt32().Should().Be(1);
        result.GetProperty("fileIds").GetInt32().Should().Be(3);
        WithoutGeneratedAt(kitAfter).Should().Be(WithoutGeneratedAt(kitBefore));
        kitAfter.GetProperty("release").GetInt32().Should().Be(1);
        kitAfter.GetProperty("logos").GetProperty("master").GetProperty("fileId").GetString().Should().NotBeNullOrEmpty();
        pinned.StatusCode.Should().Be(HttpStatusCode.OK);
        draftAfter.GetProperty("revision").GetInt32().Should().Be(draftBefore.GetProperty("revision").GetInt32());
        draftAfter.GetProperty("draft").GetRawText().Should().Be(draftBefore.GetProperty("draft").GetRawText());
        rerunExit.Should().Be(0);
        rerun.GetProperty("status").GetString().Should().Be("current");
        File.Exists(legacy).Should().BeFalse();
    }

    [Fact]
    public async Task Verb_ShouldRefuseAnUnknownArgument()
    {
        var host = target.LocalHost();
        using var output = new StringWriter();

        var exit = await LegacySqliteImportVerb.RunAsync(host.Services, ["--force"], output, TextWriter.Null, CancellationToken.None);

        exit.Should().Be(1);
        output.ToString().Should().Contain("\"status\":\"refused\"");
    }

    /// <summary>The kit as published, without the instant each read stamps.</summary>
    private static string WithoutGeneratedAt(JsonElement kit) =>
        JsonSerializer.Serialize(kit.EnumerateObject().Where(property => property.Name != "generatedAt").ToDictionary(property => property.Name, property => property.Value));

    /// <summary>Runs the verb over the host's services; its exit and the <c>import-result</c> line, which must come last.</summary>
    private static async Task<(int Exit, JsonElement Result)> RunAsync(WebApplicationFactory<Program> host, params string[] arguments)
    {
        using var output = new StringWriter();
        var exit = await LegacySqliteImportVerb.RunAsync(host.Services, arguments, output, TextWriter.Null, CancellationToken.None);
        var last = output.ToString().TrimEnd().Split('\n')[^1];
        last.Should().StartWith("import-result ");
        return (exit, JsonDocument.Parse(last["import-result ".Length..]).RootElement.Clone());
    }
}
