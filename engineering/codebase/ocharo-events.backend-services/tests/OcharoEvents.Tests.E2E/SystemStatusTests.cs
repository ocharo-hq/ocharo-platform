using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace OcharoEvents.Tests.E2E;

/// <summary>The conventional status endpoint reports the running version, anonymously.</summary>
public sealed class SystemStatusTests
{
    [Fact]
    public async Task Status_ShouldReportInformationalVersion_WhenNoSessionExists()
    {
        await using var host = EventsHost.Create(new Dictionary<string, string?>());
        using var client = EventsTestIdentity.Anonymous(host.Factory);

        using var status = await client.GetAsync("/api/system/status");

        status.StatusCode.Should().Be(HttpStatusCode.OK);
        status.Headers.CacheControl!.NoStore.Should().BeTrue();
        var body = await status.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("service").GetString().Should().Be("OcharoEvents");
        body.GetProperty("status").GetString().Should().Be("live");
        string.IsNullOrWhiteSpace(body.GetProperty("version").GetString()).Should().BeFalse();
    }
}
