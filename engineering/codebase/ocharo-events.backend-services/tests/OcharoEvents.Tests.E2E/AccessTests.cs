using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace OcharoEvents.Tests.E2E;

/// <summary>
/// Access to the Events page's mirror of WoW2 Events (events-app.md #4, OE07): no session answers 401; a route organization
/// other than the session's, or one where the person lacks <c>events:read</c>, answers 404, so another organization's
/// events are never disclosed; proposals and tracking need <c>events:track</c>. Runtime config, status, health and the
/// session route stay anonymous.
/// </summary>
public sealed class AccessTests
{
    private static readonly string OwnPath =
        $"/api/organizations/{EventsTestIdentity.Organization}/events-api/api/events/v1/catalogs/ocharo-fashion";
    private static readonly string OtherPath =
        $"/api/organizations/{EventsTestIdentity.OtherOrganization}/events-api/api/events/v1/catalogs/ocharo-fashion";
    private static readonly Guid Item = Guid.Parse("22222222-2222-4222-8222-222222222222");

    [Theory]
    [InlineData("/api/runtime-config")]
    [InlineData("/api/system/status")]
    [InlineData("/health")]
    public async Task PublicRoutes_ShouldAnswer200_WhenNoSessionExists(string path)
    {
        await using var host = EventsHost.Create(new Dictionary<string, string?>(), new RecordingEventsBrokers());
        using var client = EventsTestIdentity.Anonymous(host.Factory);

        using var response = await client.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.GetValues("X-Content-Type-Options").Single().Should().Be("nosniff");
    }

    [Fact]
    public async Task EventsRoutes_ShouldAnswer401_WhenNoSessionExists()
    {
        var events = new RecordingEventsBrokers();
        await using var host = EventsHost.Create(new Dictionary<string, string?>(), events);
        using var client = EventsTestIdentity.Anonymous(host.Factory);

        (await client.GetAsync($"{OwnPath}/tracking")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync($"{OwnPath}/tracking/summary")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync($"{OwnPath}/freshness")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync($"{OwnPath}/tracking/{Item}")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsJsonAsync($"{OwnPath}/candidates", new { facts = new { kind = "other" } })).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized);
        (await EventsTestIdentity.SessionAsync(client)).GetProperty("isAuthenticated").GetBoolean().Should().BeFalse();
        events.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task EventsRoutes_ShouldAnswer404_ForAnotherOrganizationsPath()
    {
        var events = new RecordingEventsBrokers();
        await using var host = EventsHost.Create(new Dictionary<string, string?>(), events);
        using var owner = await EventsTestIdentity.SignedIn(host.Factory, EventsTestIdentity.Owner);

        // An Ocharo owner reaching another organization's path, by list, summary, read and write.
        (await owner.GetAsync($"{OtherPath}/tracking")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await owner.GetAsync($"{OtherPath}/tracking/summary")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await owner.GetAsync($"{OtherPath}/freshness")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await owner.GetAsync($"{OtherPath}/tracking/{Item}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await owner.PostAsJsonAsync($"{OtherPath}/candidates", new { facts = new { kind = "other" } })).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
        events.Calls.Should().BeEmpty();
        (await owner.GetAsync($"{OwnPath}/tracking")).StatusCode.Should().Be(HttpStatusCode.OK);

        // Another organization's members, a person with no organization and an Ocharo member without events:read.
        foreach (var person in new[] { EventsTestIdentity.Outsider, EventsTestIdentity.Unaffiliated, EventsTestIdentity.Billing })
        {
            using var client = await EventsTestIdentity.SignedIn(host.Factory, person);
            var read = await client.GetAsync($"{OwnPath}/tracking");
            read.StatusCode.Should().Be(HttpStatusCode.NotFound, person.Subject);
            (await client.GetAsync($"{OwnPath}/tracking/{Item}")).StatusCode.Should().Be(HttpStatusCode.NotFound, person.Subject);
            (await client.GetAsync($"{OwnPath}/freshness")).StatusCode.Should().Be(HttpStatusCode.NotFound, person.Subject);
            (await CodeAsync(read)).Should().Be("NotFound");
        }
        events.Calls.Should().ContainSingle();
    }

    [Fact]
    public async Task Viewer_ShouldRead_ButNeitherProposeNorTrack()
    {
        var events = new RecordingEventsBrokers();
        await using var host = EventsHost.Create(new Dictionary<string, string?>(), events);
        using var viewer = await EventsTestIdentity.SignedIn(host.Factory, EventsTestIdentity.Viewer);

        (await viewer.GetAsync($"{OwnPath}/tracking")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await viewer.GetAsync($"{OwnPath}/tracking/{Item}")).StatusCode.Should().Be(HttpStatusCode.OK);
        var propose = await viewer.PostAsJsonAsync($"{OwnPath}/candidates", new
        {
            facts = new { title = new Dictionary<string, string> { ["und"] = "Margilan Ikat Days" }, kind = "festival" },
        });
        var track = await viewer.PutAsJsonAsync($"{OwnPath}/tracking/{Item}", new { stage = "shortlisted", expectedRevision = 1 });

        propose.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        track.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        events.Calls.Select(call => call.Method).Should().Equal("ListAsync", "GetAsync");
    }

    [Fact]
    public async Task UnsafeRequests_ShouldAnswer400_WhenCsrfTokenIsMissing()
    {
        var events = new RecordingEventsBrokers();
        await using var host = EventsHost.Create(new Dictionary<string, string?>(), events);
        using var forger = await EventsTestIdentity.SignedIn(host.Factory, EventsTestIdentity.Owner, csrf: false);

        var forged = await forger.PutAsJsonAsync($"{OwnPath}/tracking/{Item}", new { stage = "skipped" });

        forged.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await CodeAsync(forged)).Should().Be("AntiforgeryValidationFailed");
        (await forger.GetAsync($"{OwnPath}/tracking")).StatusCode.Should().Be(HttpStatusCode.OK);
        events.Of("PutAsync").Should().BeEmpty();
    }

    [Fact]
    public async Task OrganizationAndSessionRoutes_ShouldAnswerUncached()
    {
        await using var host = EventsHost.Create(new Dictionary<string, string?>(), new RecordingEventsBrokers());
        using var owner = await EventsTestIdentity.SignedIn(host.Factory, EventsTestIdentity.Owner);

        var list = await owner.GetAsync($"{OwnPath}/tracking");
        var session = await owner.GetAsync("/api/identity/session");

        list.Headers.CacheControl!.NoStore.Should().BeTrue();
        session.Headers.CacheControl!.NoStore.Should().BeTrue();
    }

    /// <summary>The problem's SDK <c>code</c>, named for its error kind.</summary>
    private static async Task<string?> CodeAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(text)) return null;
        using var document = JsonDocument.Parse(text);
        return document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("code", out var value)
            ? value.GetString() : null;
    }
}
