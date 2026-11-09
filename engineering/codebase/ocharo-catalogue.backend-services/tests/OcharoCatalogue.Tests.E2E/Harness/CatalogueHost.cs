using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace OcharoCatalogue.Tests.Harness;

/// <summary>An isolated Catalogue host with its own SQLite file and blob folder, behind the test sign-in
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
        var configuration = new Dictionary<string, string?>(settings)
        {
            ["ConnectionStrings:OcharoCatalogue"] = $"Data Source={Path.Combine(root, "test.db")};Pooling=False",
            ["Catalogue:DataRoot"] = Path.Combine(root, "data"),
            ["Catalogue:SeedOnEmpty"] = "false",
        };
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
