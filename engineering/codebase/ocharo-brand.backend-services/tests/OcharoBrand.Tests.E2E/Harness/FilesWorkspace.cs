using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OcharoBrand.Tests.Support;
using WoW.Two.Sdk.Beta.Be.Files.References;
using WoW.Two.Sdk.Beta.Be.Files.Store;
using WoW.Two.Sdk.Beta.Be.Files.Uploads;
using WoW.Two.Sdk.Beta.Be.Files.Urls;

namespace OcharoBrand.Tests.Harness;

/// <summary>One Brand store (a product database, plus a temp blob folder and seed logos) that several hosts open in turn,
/// either on <see cref="TestFiles"/> (<c>Files:BaseUrl</c> set) or local only, as a Brand moving onto WoW2 Files sees it.</summary>
internal sealed class FilesWorkspace : IAsyncDisposable
{
    private readonly string logos;
    private readonly List<WebApplicationFactory<Program>> factories = [];

    public FilesWorkspace()
    {
        Root = Path.Combine(Path.GetTempPath(), "ocharo-brand-files-tests", Guid.NewGuid().ToString("N"));
        logos = Path.Combine(Root, "logos");
        Directory.CreateDirectory(logos);
        File.WriteAllBytes(Path.Combine(logos, "och-logo-approved-ivory-v1.png"), TestArtwork.Png(10));
        File.WriteAllBytes(Path.Combine(logos, "och-logo-black-transparent-v1.png"), TestArtwork.Png(60));
        File.WriteAllBytes(Path.Combine(logos, "och-logo-white-transparent-v1.png"), TestArtwork.Png(110));
    }

    /// <summary>The temp folder every host of this workspace shares.</summary>
    public string Root { get; }

    /// <summary>The product database every host of this workspace opens.</summary>
    public string Database { get; } = BrandTestDatabase.CreateAsync().GetAwaiter().GetResult();

    /// <summary>A host whose Files brokers are <paramref name="files"/>, with <paramref name="extra"/> settings beside the
    /// Files address.</summary>
    public WebApplicationFactory<Program> Host(TestFiles files, Dictionary<string, string?>? extra = null) =>
        Factory(new(extra ?? []) { ["Files:BaseUrl"] = TestFiles.BaseUrl }, services =>
        {
            services.AddSingleton(files);
            services.AddSingleton<IFileUploadsBroker>(files);
            services.AddSingleton<IFileStoreBroker>(files);
            services.AddSingleton<IFileReferencesBroker>(files);
            services.AddSingleton<IFileUrlsBroker>(files);
        });

    /// <summary>A host with no Files address: every file stays in the local blob folder.</summary>
    public WebApplicationFactory<Program> LocalHost() => Factory([], _ => { });

    /// <summary>A host over this workspace's store; <paramref name="extra"/> settings apply last.</summary>
    public WebApplicationFactory<Program> Factory(Dictionary<string, string?> extra, Action<IServiceCollection> services)
    {
        var settings = new Dictionary<string, string?>(BrandTestIdentity.Settings(Root, Database))
        {
            ["Brand:DataRoot"] = Path.Combine(Root, "data"),
            ["Brand:SeedLogoRoot"] = logos,
        };
        foreach (var (key, value) in extra)
            settings[key] = value;
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(settings));
            builder.ConfigureServices(BrandTestIdentity.Use);
            builder.ConfigureTestServices(services);
        });
        factories.Add(factory);
        return factory;
    }

    /// <summary>Stops <paramref name="factory"/> so the next host opens the store it left behind.</summary>
    public async Task StopAsync(WebApplicationFactory<Program> factory)
    {
        await factory.DisposeAsync();
        factories.Remove(factory);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var factory in factories)
            await factory.DisposeAsync();
        try { Directory.Delete(Root, true); } catch (IOException) { }
    }
}
