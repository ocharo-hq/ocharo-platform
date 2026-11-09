using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace OcharoBrand.Tests.E2E;

/// <summary>The conventional status endpoint reports the running version, anonymously; the retired version route is gone.</summary>
public sealed class SystemStatusTests
{
    [Fact]
    public async Task Status_ShouldReportInformationalVersion_WhenNoSessionExists()
    {
        await using var host = BrandHost.Create(new Dictionary<string, string?>());
        using var client = BrandTestIdentity.Anonymous(host.Factory);

        using var status = await client.GetAsync("/api/system/status");
        using var version = await client.GetAsync("/api/system/version");

        status.StatusCode.Should().Be(HttpStatusCode.OK);
        status.Headers.CacheControl!.NoStore.Should().BeTrue();
        var body = await status.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("service").GetString().Should().Be("OcharoBrand");
        body.GetProperty("status").GetString().Should().Be("live");
        string.IsNullOrWhiteSpace(body.GetProperty("version").GetString()).Should().BeFalse();
        // An unknown private route now, so it asks for a session like any other.
        version.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
