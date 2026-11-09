using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace OcharoBrand.Tests;

/// <summary>The conventional status endpoint reports the running version, anonymously; the retired version route is gone.</summary>
public sealed class SystemStatusTests
{
    [Fact]
    public async Task Status_ShouldReportTheInformationalVersionWithoutASession()
    {
        await using var host = BrandHost.Create(new Dictionary<string, string?>());
        using var client = BrandTestIdentity.Anonymous(host.Factory);

        using var status = await client.GetAsync("/api/system/status");
        using var version = await client.GetAsync("/api/system/version");

        Assert.Equal(HttpStatusCode.OK, status.StatusCode);
        Assert.True(status.Headers.CacheControl!.NoStore);
        var body = await status.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("OcharoBrand", body.GetProperty("service").GetString());
        Assert.Equal("live", body.GetProperty("status").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("version").GetString()));
        // An unknown private route now, so it asks for a session like any other.
        Assert.Equal(HttpStatusCode.Unauthorized, version.StatusCode);
    }
}
