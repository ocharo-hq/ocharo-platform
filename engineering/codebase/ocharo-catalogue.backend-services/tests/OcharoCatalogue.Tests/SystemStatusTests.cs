using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace OcharoCatalogue.Tests;

/// <summary>The conventional status endpoint reports the running version, anonymously, beside the retained version route.</summary>
public sealed class SystemStatusTests
{
    [Fact]
    public async Task Status_ShouldReportTheInformationalVersionWithoutASession()
    {
        await using var host = CatalogueHost.Create(new Dictionary<string, string?>());
        using var client = CatalogueSignIn.CreateClient(host.Factory, person: null);

        using var status = await client.GetAsync("/api/system/status");
        using var version = await client.GetAsync("/api/system/version");

        Assert.Equal(HttpStatusCode.OK, status.StatusCode);
        Assert.True(status.Headers.CacheControl!.NoStore);
        var body = await status.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("OcharoCatalogue", body.GetProperty("service").GetString());
        Assert.Equal("live", body.GetProperty("status").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("version").GetString()));
        Assert.Equal(HttpStatusCode.OK, version.StatusCode);
        Assert.Equal(body.GetProperty("version").GetString(),
            (await version.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("version").GetString());
    }
}
