using AwesomeAssertions;
using BrandWorkspace.Api.Configurations;
using BrandWorkspace.Api.Identity.Telegram;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace BrandWorkspace.Tests.E2E;

/// <summary>The Desk host reads its settings files through the SDK and keeps local conveniences to a local run.</summary>
public sealed class LocalRunTests
{
    private static readonly string[] Telegram =
    [
        "--Identity:Telegram:Enabled=true", "--Identity:Telegram:BotUsername=ocharo_test_bot",
        "--Identity:Telegram:BotToken=123456789:synthetic_token_for_offline_tests_ABC", "--Identity:Telegram:PollingEnabled=true"
    ];

    [Fact]
    public void LocalRun_ReadsTheMachineFileOnlyWhenTheLaunchProfileAsks()
    {
        WithContentRoot(root =>
        {
            File.WriteAllText(Path.Combine(root, "appsettings.Local.json"), Source("machine"));

            SourceRoot(root, "--LOCAL_SETTINGS=true").Should().Be("machine");
            SourceRoot(root).Should().BeNull("a test or container run never sets LOCAL_SETTINGS");
        });
    }

    [Fact]
    public void DeployedDev_ReadsTheTargetFileAndNeverTheMachineFile()
    {
        WithContentRoot(root =>
        {
            File.WriteAllText(Path.Combine(root, "appsettings.Local.json"), Source("machine"));
            File.WriteAllText(Path.Combine(root, "appsettings.Target.json"), Source("target"));

            SourceRoot(root, "--DEPLOY_ENVIRONMENT=dev", "--LOCAL_SETTINGS=true").Should().Be("target");
        });
    }

    [Fact]
    public async Task DeployedDev_RefusesPollingAndAKeyRingWithoutDurableStorage()
    {
        await using var app = Build(["--DEPLOY_ENVIRONMENT=dev", .. Telegram]);

        var polling = () => app.Services.GetRequiredService<IOptions<TelegramAuthSettings>>().Value;
        polling.Should().Throw<OptionsValidationException>().WithMessage("*only on a local run*");
        var keys = () => app.Services.GetRequiredService<IOptions<KeyManagementOptions>>().Value;
        keys.Should().Throw<InvalidOperationException>().WithMessage("DataProtection:KeysPath must name durable storage.");
    }

    [Fact]
    public async Task LocalRun_KeepsPollingAndTheMachineKeyRing()
    {
        await using var app = Build(Telegram);

        app.Services.GetRequiredService<IOptions<TelegramAuthSettings>>().Value.PollingEnabled.Should().BeTrue();
        app.Services.GetRequiredService<IOptions<KeyManagementOptions>>().Value.XmlRepository.Should().NotBeNull();
    }

    private static WebApplication Build(string[] args, string? contentRoot = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args, EnvironmentName = Environments.Development, ContentRootPath = contentRoot ?? AppContext.BaseDirectory
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
