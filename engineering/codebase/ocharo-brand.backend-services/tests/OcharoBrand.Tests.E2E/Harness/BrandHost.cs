using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Core;

namespace OcharoBrand.Tests.Harness;

/// <summary>An isolated Brand host with its own SQLite file, blob folder and key ring, signed in through
/// <see cref="BrandTestIdentity"/>; <c>settings</c> apply last, so a test may change or clear (null) any default.</summary>
/// <remarks>The seed folder does not exist, so an empty store seeds without logos and no real artwork is read.</remarks>
internal sealed class BrandHost : IAsyncDisposable
{
    private readonly string root;

    private BrandHost(string root, WebApplicationFactory<Program> factory)
    {
        this.root = root;
        Factory = factory;
    }

    public WebApplicationFactory<Program> Factory { get; }

    public static BrandHost Create(IReadOnlyDictionary<string, string?> settings, CapturedLogs? logs = null)
    {
        var root = Path.Combine(Path.GetTempPath(), "ocharo-brand-host", Guid.NewGuid().ToString("N"));
        var configuration = BrandTestIdentity.Settings(root);
        configuration["ConnectionStrings:OcharoBrand"] = $"Data Source={Path.Combine(root, "test.db")};Pooling=False";
        configuration["Brand:DataRoot"] = Path.Combine(root, "data");
        configuration["Brand:SeedLogoRoot"] = Path.Combine(root, "missing-logos");
        foreach (var (key, value) in settings)
            configuration[key] = value;
        Directory.CreateDirectory(root);
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(configuration));
            builder.ConfigureServices(services =>
            {
                BrandTestIdentity.Use(services);
                if (logs is not null) services.AddSingleton<ILogEventSink>(logs);
            });
        });
        return new BrandHost(root, factory);
    }

    public async ValueTask DisposeAsync()
    {
        await Factory.DisposeAsync();
        try { Directory.Delete(root, true); } catch (IOException) { }
    }
}
