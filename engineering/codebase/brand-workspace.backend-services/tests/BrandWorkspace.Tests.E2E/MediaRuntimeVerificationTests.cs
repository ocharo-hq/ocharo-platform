using AwesomeAssertions;
using BrandWorkspace.Api.Configurations;
using WoW.Two.Sdk.Beta.Be.Meta;
using Xunit;

namespace BrandWorkspace.Tests.E2E;

/// <summary>Exercises the same native decoder probe executed while building the Linux image.</summary>
public sealed class MediaRuntimeVerificationTests
{
    [Fact]
    public async Task Probe_ShouldDecodeAndExit_WhenNoDatabaseOrKeysAreConfigured()
    {
        // The very dispatch the image build runs (`dotnet BrandWorkspace.Api.dll media verify`), with no database or
        // key ring.
        var configured = false;
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exit = await ServiceHost.RunAsync(["media", "verify"], builder => builder.Configure(), _ =>
        {
            configured = true;
            return Task.CompletedTask;
        }, verbs => verbs.AddDeskVerbs(), output, error, CancellationToken.None);

        exit.Should().Be(0, error.ToString());
        configured.Should().BeFalse("a verb never configures or serves the host");
        var result = System.Text.Json.JsonDocument.Parse(output.ToString()).RootElement;
        result.GetProperty("mediaRuntime").GetString().Should().Be("ready");
        result.GetProperty("contentType").GetString().Should().Be("image/png");
        result
            .GetProperty("sha256")
            .GetString()
            .Should()
            .Be("5ce44d3259b5bf4b369481e556ceb140f338020b55a8a7ad70214b24da15bfe6");
    }
}
