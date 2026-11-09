using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace OcharoCatalogue.Tests.Harness;

/// <summary>An isolated Catalogue host with its own PostgreSQL database and blob folder, behind the test sign-in
/// (<see cref="CatalogueSignIn"/>).</summary>
public sealed class CatalogueHost : IAsyncDisposable
{
    private readonly string root;

    private CatalogueHost(string root, WebApplicationFactory<Program> factory)
    {
        this.root = root;
        Factory = factory;
    }

    public WebApplicationFactory<Program> Factory { get; }

    public static CatalogueHost Create(IReadOnlyDictionary<string, string?> settings)
    {
        var root = Path.Combine(Path.GetTempPath(), "ocharo-catalogue-host", Guid.NewGuid().ToString("N"));
        // The test's own settings win, so two hosts may share one database, as a restart does.
        var configuration = new Dictionary<string, string?>
        {
            [CatalogueTestDatabase.ConnectionKey] = settings.TryGetValue(CatalogueTestDatabase.ConnectionKey, out var database)
                ? database
                : CatalogueTestDatabase.For(Path.Combine(root, "test.db")),
            [CatalogueTestDatabase.OwnerKey] = CatalogueTestDatabase.OwnerInstance,
            ["Catalogue:DataRoot"] = Path.Combine(root, "data"),
            ["Catalogue:SeedOnEmpty"] = "false",
        };
        foreach (var (key, value) in settings)
            configuration[key] = value;
        Directory.CreateDirectory(root);
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseCatalogueSignIn();
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(configuration));
        });
        return new CatalogueHost(root, factory);
    }

    public async ValueTask DisposeAsync()
    {
        await Factory.DisposeAsync();
        try { Directory.Delete(root, true); } catch (IOException) { }
    }
}
