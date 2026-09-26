using AwesomeAssertions;
using BrandWorkspace.Api.Configurations;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace BrandWorkspace.Tests.E2E;

/// <summary>Exercises the same native decoder probe executed while building the Linux image.</summary>
public sealed class MediaRuntimeVerificationTests
{
    [Fact]
    public async Task Probe_ShouldDecodeAndExit_WithoutStartingTheHostOrRequiringDatabaseAndKeys()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Production });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Database"] = "Host=127.0.0.1;Port=1;Database=unavailable;Username=unused;Timeout=1"
        });
        builder.Configure();
        builder.Logging.ClearProviders();
        var app = builder.Build();
        app.Configure(serveHttp: false);
        var started = false;
        app.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStarted.Register(() => started = true);
        var originalOutput = Console.Out;
        using var output = new StringWriter();
        try
        {
            Console.SetOut(output);
            await app.RunCommandOrHostAsync(["--verify-media-runtime"]);
        }
        finally { Console.SetOut(originalOutput); }
        started.Should().BeFalse();
        var result = System.Text.Json.JsonDocument.Parse(output.ToString()).RootElement;
        result.GetProperty("mediaRuntime").GetString().Should().Be("ready");
        result.GetProperty("contentType").GetString().Should().Be("image/png");
        result.GetProperty("sha256").GetString().Should().Be("5ce44d3259b5bf4b369481e556ceb140f338020b55a8a7ad70214b24da15bfe6");
    }
}
