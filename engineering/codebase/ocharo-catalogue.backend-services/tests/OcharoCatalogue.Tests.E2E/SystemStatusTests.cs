using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace OcharoCatalogue.Tests.E2E;

/// <summary>The conventional status endpoint reports the running version, anonymously; the retired version route is gone.</summary>
public sealed class SystemStatusTests
{
    [Fact]
    public async Task Status_ShouldReportInformationalVersion_WhenNoSessionExists()
    {
        await using var host = CatalogueHost.Create(new Dictionary<string, string?>());
        using var client = CatalogueSignIn.CreateClient(host.Factory, person: null);

        using var status = await client.GetAsync("/api/system/status");
        using var retired = await client.GetAsync("/api/system/version");

        status.StatusCode.Should().Be(HttpStatusCode.OK);
        status.Headers.CacheControl!.NoStore.Should().BeTrue();
        var body = await status.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("service").GetString().Should().Be("OcharoCatalogue");
        body.GetProperty("status").GetString().Should().Be("live");
        string.IsNullOrWhiteSpace(body.GetProperty("version").GetString()).Should().BeFalse();
        retired.StatusCode.Should().Be(HttpStatusCode.NotFound);
        retired.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");
    }
}
