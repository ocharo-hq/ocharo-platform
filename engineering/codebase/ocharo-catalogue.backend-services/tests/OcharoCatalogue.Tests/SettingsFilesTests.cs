using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace OcharoCatalogue.Tests;

/// <summary>The host reads the machine and target settings files through the SDK and exposes OpenAPI only on a local run.</summary>
public sealed class SettingsFilesTests
{
    [Fact]
    public async Task Local_run_with_local_settings_reads_the_machine_file_and_exposes_openapi()
    {
        await WithContentRoot(async root =>
        {
            File.WriteAllText(Path.Combine(root, "appsettings.Local.json"), Source("missing-machine-source"));
            await using var factory = Host(root, deployEnvironment: null);

            Assert.Equal("missing-machine-source", SourceRoot(factory));
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/openapi/v1.json")).StatusCode);
        });
    }

    [Fact]
    public async Task Deployed_dev_reads_the_target_file_and_hides_openapi()
    {
        await WithContentRoot(async root =>
        {
            File.WriteAllText(Path.Combine(root, "appsettings.Local.json"), Source("missing-machine-source"));
            File.WriteAllText(Path.Combine(root, "appsettings.Target.json"), Source("missing-target-source"));
            await using var factory = Host(root, deployEnvironment: "dev");

            Assert.Equal("missing-target-source", SourceRoot(factory));
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
            Assert.NotEqual(HttpStatusCode.OK, (await client.GetAsync("/openapi/v1.json")).StatusCode);
        });
    }

    private static WebApplicationFactory<Program> Host(string root, string? deployEnvironment) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseContentRoot(root);
            builder.UseSetting("LOCAL_SETTINGS", "true");
            if (deployEnvironment is not null) builder.UseSetting("DEPLOY_ENVIRONMENT", deployEnvironment);
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:OcharoCatalogue"] = $"Data Source={Path.Combine(root, "api.db")};Pooling=False",
                ["Catalogue:DataRoot"] = Path.Combine(root, "data"),
            }));
        });

    private static string? SourceRoot(WebApplicationFactory<Program> factory) =>
        factory.Services.GetRequiredService<IConfiguration>()["Catalogue:SourceRoot"];

    private static string Source(string folder) =>
        $$"""{ "Catalogue": { "SourceRoot": "{{folder}}" } }""";

    private static async Task WithContentRoot(Func<string, Task> test)
    {
        var root = Path.Combine(Path.GetTempPath(), "ocharo-catalogue-settings", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try { await test(root); }
        finally { try { Directory.Delete(root, true); } catch (IOException) { } }
    }
}
