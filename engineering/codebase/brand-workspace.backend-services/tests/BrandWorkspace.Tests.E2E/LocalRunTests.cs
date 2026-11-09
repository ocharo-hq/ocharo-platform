using AwesomeAssertions;
using BrandWorkspace.Api.Configurations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace BrandWorkspace.Tests.E2E;

/// <summary>
/// The Desk host reads its settings files through the SDK and keeps local conveniences to a local run.
/// </summary>
public sealed class LocalRunTests
{
    [Fact]
    public void LocalRun_ShouldReadTheMachineFileOnly_WhenTheLaunchProfileAsks()
    {
        WithContentRoot(root =>
        {
            File.WriteAllText(Path.Combine(root, "appsettings.Local.json"), Source("machine"));

            SourceRoot(root, "--LOCAL_SETTINGS=true").Should().Be("machine");
            SourceRoot(root).Should().BeNull("a test or container run never sets LOCAL_SETTINGS");
        });
    }

    [Fact]
    public void DeployedDev_ShouldReadTheTargetFileAndNeverTheMachineFile()
    {
        WithContentRoot(root =>
        {
            File.WriteAllText(Path.Combine(root, "appsettings.Local.json"), Source("machine"));
            File.WriteAllText(Path.Combine(root, "appsettings.Target.json"), Source("target"));

            SourceRoot(root, "--DEPLOY_ENVIRONMENT=dev", "--LOCAL_SETTINGS=true").Should().Be("target");
        });
    }

    [Fact]
    public async Task DeployedDev_ShouldRefuseTheKeyRing_WhenItHasNoDurableStorage()
    {
        await using var app = Build(["--DEPLOY_ENVIRONMENT=dev"]);

        var keys = () => app.Services.GetRequiredService<IOptions<KeyManagementOptions>>().Value;
        keys.Should().Throw<OptionsValidationException>().WithMessage("*KeyDirectory is required outside a local run*");
    }

    [Fact]
    public async Task LocalRun_ShouldKeepTheMachineKeyRing()
    {
        await using var app = Build([]);

        app.Services.GetRequiredService<IOptions<KeyManagementOptions>>().Value.XmlRepository.Should().NotBeNull();
    }

    private static WebApplication Build(string[] args, string? contentRoot = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = DeskTestHost.ApplicationName,
            Args = args,
            EnvironmentName = Environments.Development,
            ContentRootPath = contentRoot ?? AppContext.BaseDirectory
        });
        builder.Configure();
        return builder.Build();
    }

    private static string? SourceRoot(string root, params string[] args)
    {
        var app = Build(args, root);
        try { return app.Services.GetRequiredService<IConfiguration>()["Commerce:SourceRoot"]; }
        finally { ((IDisposable)app).Dispose(); }
    }

    private static string Source(string value) => $$"""{ "Commerce": { "SourceRoot": "{{value}}" } }""";

    private static void WithContentRoot(Action<string> test)
    {
        var root = Path.Combine(Path.GetTempPath(), "brand-workspace-settings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try { test(root); }
        finally { Directory.Delete(root, true); }
    }
}
