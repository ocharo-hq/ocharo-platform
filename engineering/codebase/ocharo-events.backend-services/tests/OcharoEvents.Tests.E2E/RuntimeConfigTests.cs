using System.Net;
using System.Text.Json;

namespace OcharoEvents.Tests.E2E;

/// <summary>The Events host serves the Ocharo app addresses as public runtime settings, anonymously and uncached: Brand's
/// keys plus <c>OCHARO_EVENTS_URL</c> (events-app.md #7).</summary>
public sealed class RuntimeConfigTests
{
    [Fact]
    public async Task RuntimeConfig_ShouldServeTheEventsAddress_WhenItIsSet()
    {
        await using var host = EventsHost.Create(new Dictionary<string, string?>
        {
            ["OcharoApps:Desk"] = "https://ocharo-desk-dev.example.ts.net/",
            ["OcharoApps:Brand"] = "https://ocharo-brand-dev.example.ts.net/",
            ["OcharoApps:Events"] = "https://ocharo-events-dev.example.ts.net/",
        });
        using var client = EventsTestIdentity.Anonymous(host.Factory);

        using var response = await client.GetAsync("/api/runtime-config");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        (await ReadAsync(response)).Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["OCHARO_DESK_URL"] = "https://ocharo-desk-dev.example.ts.net/",
            ["OCHARO_BRAND_URL"] = "https://ocharo-brand-dev.example.ts.net/",
            ["OCHARO_EVENTS_URL"] = "https://ocharo-events-dev.example.ts.net/",
        });
    }

    [Fact]
    public async Task RuntimeConfig_ShouldLeaveTheEventsAddressOut_WhenItIsUnsetOrNotPlainHttps()
    {
        await using var unset = EventsHost.Create(new Dictionary<string, string?>());
        await using var rejected = EventsHost.Create(new Dictionary<string, string?>
        {
            ["OcharoApps:Events"] = "http://ocharo-events-dev.example.ts.net/",
        });

        using var empty = await EventsTestIdentity.Anonymous(unset.Factory).GetAsync("/api/runtime-config");
        using var dropped = await EventsTestIdentity.Anonymous(rejected.Factory).GetAsync("/api/runtime-config");

        (await empty.Content.ReadAsStringAsync()).Should().Be("{}");
        (await ReadAsync(dropped)).Should().NotContainKey("OCHARO_EVENTS_URL");
    }

    private static async Task<Dictionary<string, string>> ReadAsync(HttpResponseMessage response)
    {
        return JsonSerializer.Deserialize<Dictionary<string, string>>(await response.Content.ReadAsStringAsync())!;
    }
}
