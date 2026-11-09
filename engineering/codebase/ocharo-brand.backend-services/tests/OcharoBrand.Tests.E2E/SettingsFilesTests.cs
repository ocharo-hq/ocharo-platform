using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace OcharoBrand.Tests.E2E;

/// <summary>The host reads the machine and target settings files through the SDK and exposes OpenAPI only on a local run.</summary>
public sealed class SettingsFilesTests
{
    [Fact]
    public async Task LocalRun_ShouldReadMachineFileAndExposeOpenApi_WhenLocalSettingsAreOn()
    {
        await WithContentRoot(async root =>
        {
            File.WriteAllText(Path.Combine(root, "appsettings.Local.json"), Source("missing-machine-logos"));
            await using var factory = Host(root, deployEnvironment: null);

            SeedLogoRoot(factory).Should().Be("missing-machine-logos");
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
            (await client.GetAsync("/openapi/v1.json")).StatusCode.Should().Be(HttpStatusCode.OK);
        });
    }

    [Fact]
    public async Task DeployedDev_ShouldReadTargetFileAndHideOpenApi_WhenTargetFileExists()
    {
        await WithContentRoot(async root =>
        {
            File.WriteAllText(Path.Combine(root, "appsettings.Local.json"), Source("missing-machine-logos"));
            File.WriteAllText(Path.Combine(root, "appsettings.Target.json"), Source("missing-target-logos"));
            await using var factory = Host(root, deployEnvironment: "dev");

            SeedLogoRoot(factory).Should().Be("missing-target-logos");
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
            (await client.GetAsync("/openapi/v1.json")).StatusCode.Should().NotBe(HttpStatusCode.OK);
        });
    }

    private static WebApplicationFactory<Program> Host(string root, string? deployEnvironment) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseContentRoot(root);
            builder.UseSetting("LOCAL_SETTINGS", "true");
            if (deployEnvironment is not null) builder.UseSetting("DEPLOY_ENVIRONMENT", deployEnvironment);
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>(BrandTestIdentity.Settings(root))
            {
                ["Brand:DataRoot"] = Path.Combine(root, "data"),
            }));
            builder.ConfigureServices(BrandTestIdentity.Use);
        });

    private static string? SeedLogoRoot(WebApplicationFactory<Program> factory) =>
        factory.Services.GetRequiredService<IConfiguration>()["Brand:SeedLogoRoot"];

    private static string Source(string folder) =>
        $$"""{ "Brand": { "SeedLogoRoot": "{{folder}}" } }""";

    private static async Task WithContentRoot(Func<string, Task> test)
    {
        var root = Path.Combine(Path.GetTempPath(), "ocharo-brand-settings", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try { await test(root); }
        finally { try { Directory.Delete(root, true); } catch (IOException) { } }
    }
}
