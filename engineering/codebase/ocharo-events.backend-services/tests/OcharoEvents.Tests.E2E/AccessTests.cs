using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;

namespace OcharoEvents.Tests.E2E;

/// <summary>
/// Access to the Events routes (events-app.md #4): no session answers 401; a route organization other than the session's,
/// or one where the person lacks <c>events:read</c>, answers 404, so another organization's events are never disclosed;
/// proposals and tracking need <c>events:track</c>. Runtime config, status, health and the session route stay anonymous.
/// </summary>
public sealed class AccessTests
{
    private static readonly string OwnPath = $"/api/organizations/{EventsTestIdentity.Organization}/events";
    private static readonly string OtherPath = $"/api/organizations/{EventsTestIdentity.OtherOrganization}/events";

    [Theory]
    [InlineData("/api/runtime-config")]
    [InlineData("/api/system/status")]
    [InlineData("/health")]
    public async Task PublicRoutes_ShouldAnswer200_WhenNoSessionExists(string path)
    {
        await using var host = EventsHost.Create(new Dictionary<string, string?>(), new FakeEventsService());
        using var client = EventsTestIdentity.Anonymous(host.Factory);

        using var response = await client.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.GetValues("X-Content-Type-Options").Single().Should().Be("nosniff");
    }

    [Fact]
    public async Task EventsRoutes_ShouldAnswer401_WhenNoSessionExists()
    {
        var events = new FakeEventsService();
        await using var host = EventsHost.Create(new Dictionary<string, string?>(), events);
        using var client = EventsTestIdentity.Anonymous(host.Factory);

        (await client.GetAsync(OwnPath)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync($"{OwnPath}/summary")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync($"{OwnPath}/{Guid.NewGuid()}")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsJsonAsync(OwnPath, new { title = "x" })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await EventsTestIdentity.SessionAsync(client)).GetProperty("isAuthenticated").GetBoolean().Should().BeFalse();
        events.TrackingWrites.Should().BeEmpty();
    }

    [Fact]
    public async Task EventsRoutes_ShouldAnswer404_ForAnotherOrganizationsPath()
    {
        var events = new FakeEventsService();
        var item = events.Add(Fair("Tashkent Fashion Week"));
        await using var host = EventsHost.Create(new Dictionary<string, string?>(), events);
        using var owner = await EventsTestIdentity.SignedIn(host.Factory, EventsTestIdentity.Owner);

        // An Ocharo owner reaching another organization's path, by list, summary, read and write.
        (await owner.GetAsync(OtherPath)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await owner.GetAsync($"{OtherPath}/summary")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await owner.GetAsync($"{OtherPath}/{item.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await owner.PostAsJsonAsync(OtherPath, new { title = "x", scope = "local", format = "other" })).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
        (await owner.GetAsync(OwnPath)).StatusCode.Should().Be(HttpStatusCode.OK);

        // Another organization's members, a person with no organization and an Ocharo member without events:read.
        foreach (var person in new[] { EventsTestIdentity.Outsider, EventsTestIdentity.Unaffiliated, EventsTestIdentity.Billing })
        {
            using var client = await EventsTestIdentity.SignedIn(host.Factory, person);
            var read = await client.GetAsync(OwnPath);
            read.StatusCode.Should().Be(HttpStatusCode.NotFound, person.Subject);
            (await client.GetAsync($"{OwnPath}/{item.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound, person.Subject);
            (await CodeAsync(read)).Should().Be("NotFound");
        }
        events.TrackingWrites.Should().BeEmpty();
    }

    [Fact]
    public async Task Viewer_ShouldRead_ButNeitherProposeNorTrack()
    {
        var events = new FakeEventsService();
        var item = events.Add(Fair("Tashkent Fashion Week"));
        await using var host = EventsHost.Create(new Dictionary<string, string?>(), events);
        using var viewer = await EventsTestIdentity.SignedIn(host.Factory, EventsTestIdentity.Viewer);

        (await viewer.GetAsync(OwnPath)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await viewer.GetAsync($"{OwnPath}/{item.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        var propose = await viewer.PostAsJsonAsync(OwnPath, new
        {
            title = "Margilan Ikat Days", scope = "local", format = "festival", country = "UZ", datePrecision = "unknown",
        });
        var track = await viewer.PutAsJsonAsync($"{OwnPath}/{item.Id}", new
        {
            expectedRevision = 1, title = "Tashkent Fashion Week", scope = "local", format = "runway", country = "UZ",
            startsOn = "2026-12-03", datePrecision = "day", status = "shortlisted",
        });

        propose.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        track.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        events.TrackingWrites.Should().BeEmpty();
    }

    [Fact]
    public async Task UnsafeRequests_ShouldAnswer400_WhenCsrfTokenIsMissing()
    {
        var events = new FakeEventsService();
        await using var host = EventsHost.Create(new Dictionary<string, string?>(), events);
        using var forger = await EventsTestIdentity.SignedIn(host.Factory, EventsTestIdentity.Owner, csrf: false);

        var forged = await forger.PostAsJsonAsync(OwnPath, new { title = "x", scope = "local", format = "other" });

        forged.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await CodeAsync(forged)).Should().Be("AntiforgeryValidationFailed");
        (await forger.GetAsync(OwnPath)).StatusCode.Should().Be(HttpStatusCode.OK);
        events.TrackingWrites.Should().BeEmpty();
    }

    [Fact]
    public async Task OrganizationAndSessionRoutes_ShouldAnswerUncached()
    {
        await using var host = EventsHost.Create(new Dictionary<string, string?>(), new FakeEventsService());
        using var owner = await EventsTestIdentity.SignedIn(host.Factory, EventsTestIdentity.Owner);

        var list = await owner.GetAsync(OwnPath);
        var session = await owner.GetAsync("/api/identity/session");

        list.Headers.CacheControl!.NoStore.Should().BeTrue();
        session.Headers.CacheControl!.NoStore.Should().BeTrue();
    }

    private static EventFacts Fair(string title) => new()
    {
        Title = new Dictionary<string, string> { ["und"] = title },
        Kind = "runway",
        Place = new EventPlace { City = "Tashkent", Country = "UZ" },
        Starts = new EventWhen { Value = "2026-12-03", Precision = DatePrecision.Day },
    };

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
