using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OcharoEvents.Application.Events.Repositories;
using OcharoEvents.Infrastructure.Events.Services;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Client;
using WoW.Two.Sdk.Beta.Be.Http.Auth.OAuth2TokenExchange;

namespace OcharoEvents.Tests.E2E;

/// <summary>
/// The Events page's routes on WoW2 Events, moved from Desk with their wire unchanged (events-app.md #3; events-adoption.md
/// #7, #8): an event accepted in Events shows on the page, tracking writes pass, the old fact writes answer 410, and a
/// member of one organization never reads another's events.
/// </summary>
public sealed class EventsServiceRouteTests
{
    private static readonly string Path = $"/api/organizations/{EventsTestIdentity.Organization}/events";

    [Fact]
    public async Task EventsPage_ShouldServeEvents_TrackThroughTheService_RefuseOldWrites_AndStayInsideItsOrganization()
    {
        var service = new FakeEventsService();
        EventModel accepted = service.Add(new EventFacts
        {
            Title = new Dictionary<string, string> { ["und"] = "Tashkent Fashion Week" },
            Kind = "runway",
            Place = new EventPlace { City = "Tashkent", Country = "UZ" },
            Starts = new EventWhen { Value = "2026-12-03", Precision = DatePrecision.Day },
        });
        await using var host = EventsHost.Create(new Dictionary<string, string?>(), service);
        using HttpClient client = await EventsTestIdentity.SignedIn(host.Factory, EventsTestIdentity.Owner);
        using HttpClient strangerClient = await EventsTestIdentity.SignedIn(host.Factory, EventsTestIdentity.Outsider);

        JsonElement listed = (await DataAsync(await client.GetAsync(Path))).EnumerateArray().Single();
        listed.GetProperty("id").GetGuid().Should().Be(accepted.Id);
        listed.GetProperty("status").GetString().Should().Be("candidate");
        listed.GetProperty("scope").GetString().Should().Be("local");
        listed.GetProperty("revision").GetInt32().Should().Be(1);

        JsonElement tracked = await DataAsync(await client.PutAsJsonAsync($"{Path}/{accepted.Id}",
            Body(listed, 1, status: "shortlisted")));
        tracked.GetProperty("status").GetString().Should().Be("shortlisted");
        tracked.GetProperty("revision").GetInt32().Should().Be(2);
        service.TrackingWrites.Should().ContainSingle().Which.Stage.Should().Be("shortlisted");

        (await client.PutAsJsonAsync($"{Path}/{accepted.Id}", Body(listed, 2, title: "Renamed"))).StatusCode
            .Should().Be(HttpStatusCode.Gone);
        (await client.DeleteAsync($"{Path}/{accepted.Id}?expectedRevision=2")).StatusCode
            .Should().Be(HttpStatusCode.Gone);

        JsonElement proposed = await DataAsync(await client.PostAsJsonAsync(Path, new
        {
            title = "Margilan Ikat Days", scope = "local", format = "festival", country = "UZ",
            datePrecision = "unknown", participation = "", notes = "",
        }));
        service.Event(proposed.GetProperty("id").GetGuid()).Review.Should().Be(EventReviewState.Candidate);

        (await strangerClient.GetAsync(Path)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await strangerClient.PutAsJsonAsync($"{Path}/{accepted.Id}", Body(listed, 2, status: "skipped"))).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
        service.TrackingWrites.Should().HaveCount(2);
    }

    [Fact]
    public async Task StageSave_ShouldReachEvents_WhenTheEventsNamesExceedDesksOwnTable()
    {
        var service = new FakeEventsService();
        EventModel imported = service.Add(new EventFacts
        {
            Title = new Dictionary<string, string> { ["und"] = "Long-named fair" },
            Kind = "trade-fair",
            Organizer = new EventOrganizer { Name = new string('o', 180) },
            Place = new EventPlace { City = new string('c', 150), Country = "DE" },
            Starts = new EventWhen { Value = "2027-01-29", Precision = DatePrecision.Day },
        });
        await using var host = EventsHost.Create(new Dictionary<string, string?>(), service);
        using HttpClient client = await EventsTestIdentity.SignedIn(host.Factory, EventsTestIdentity.Editor);
        JsonElement listed = (await DataAsync(await client.GetAsync(Path))).EnumerateArray().Single();

        JsonElement tracked = await DataAsync(await client.PutAsJsonAsync($"{Path}/{imported.Id}", new
        {
            expectedRevision = 1,
            title = listed.GetProperty("title").GetString(),
            organizer = listed.GetProperty("organizer").GetString(),
            scope = listed.GetProperty("scope").GetString(),
            format = listed.GetProperty("format").GetString(),
            country = listed.GetProperty("country").GetString(),
            city = listed.GetProperty("city").GetString(),
            startsOn = listed.GetProperty("startsOn").GetString(),
            datePrecision = listed.GetProperty("datePrecision").GetString(),
            participation = listed.GetProperty("participation").GetString(),
            status = "applying",
            priority = "high",
            notes = "Book a stand.",
        }));

        tracked.GetProperty("status").GetString().Should().Be("applying");
        tracked.GetProperty("organizer").GetString().Should().HaveLength(180);
        service.TrackingWrites.Should().ContainSingle().Which.Stage.Should().Be("applying");
    }

    [Fact]
    public async Task Summary_ShouldCountEveryStatus_AndListTheNearestDeadlines()
    {
        var service = new FakeEventsService();
        EventModel open = service.Add(new EventFacts
        {
            Title = new Dictionary<string, string> { ["und"] = "Ambiente Frankfurt" },
            Kind = "trade-fair",
            Place = new EventPlace { Country = "DE" },
            Starts = new EventWhen { Value = "2027-01-29", Precision = DatePrecision.Day },
            Deadlines = [new EventDeadline { Kind = DeadlineKind.Application, At = new EventWhen { Value = "2026-12-01", Precision = DatePrecision.Day } }],
        });
        service.Track(open.Id, "applying");
        await using var host = EventsHost.Create(new Dictionary<string, string?>(), service);
        using HttpClient client = await EventsTestIdentity.SignedIn(host.Factory, EventsTestIdentity.Viewer);

        JsonElement summary = await DataAsync(await client.GetAsync($"{Path}/summary?today=2026-11-10"));

        summary.GetProperty("byStatus").GetProperty("applying").GetInt32().Should().Be(1);
        summary.GetProperty("byStatus").GetProperty("candidate").GetInt32().Should().Be(0);
        summary.GetProperty("upcomingDeadlines").EnumerateArray().Single().GetProperty("id").GetGuid().Should().Be(open.Id);
    }

    [Fact]
    public async Task Host_ShouldWireEventsByItsOwnIdentityClient_WithNoStoreOfItsOwn()
    {
        await using var host = EventsHost.Create(new Dictionary<string, string?>());
        using var anonymous = EventsTestIdentity.Anonymous(host.Factory);
        (await anonymous.GetAsync("/health")).StatusCode.Should().Be(HttpStatusCode.OK);

        EventsClientOptions events = host.Factory.Services.GetRequiredService<IOptions<EventsClientOptions>>().Value;
        events.BaseAddress.Should().Be(new Uri(EventsHost.EventsUrl));
        events.Identity!.TokenEndpoint.Should().Be(new Uri("https://identity.example.test/connect/token"));
        events.Identity.ClientId.Should().Be("ocharo-events");
        host.Factory.Services.GetRequiredService<IOptions<EventsServiceOptions>>().Value.Catalog.Should().Be("ocharo-fashion");
        host.Factory.Services.GetRequiredService<IOAuth2SubjectTokenSource>().GetType().FullName
            .Should().Be("WoW.Two.Sdk.Beta.Be.Identity.Bff.Sessions.BffSubjectTokenSource");
        using var scope = host.Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IEventRepository>().Should().BeOfType<EventsServiceRepository>();
    }

    [Fact]
    public async Task EventsPage_ShouldAnswerUnavailable_InALocalRunWithoutEvents()
    {
        await using var host = EventsHost.Create(new Dictionary<string, string?> { ["Events:BaseUrl"] = null }, environment: "Development");
        using HttpClient client = await EventsTestIdentity.SignedIn(host.Factory, EventsTestIdentity.Owner);

        (await client.GetAsync(Path)).StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    private static object Body(JsonElement item, int expectedRevision, string? status = null, string? title = null) => new
    {
        expectedRevision,
        title = title ?? item.GetProperty("title").GetString(),
        scope = item.GetProperty("scope").GetString(),
        format = item.GetProperty("format").GetString(),
        country = item.GetProperty("country").GetString(),
        city = item.GetProperty("city").GetString(),
        startsOn = item.GetProperty("startsOn").GetString(),
        datePrecision = item.GetProperty("datePrecision").GetString(),
        participation = item.GetProperty("participation").GetString(),
        status = status ?? item.GetProperty("status").GetString(),
        priority = item.GetProperty("priority").GetString(),
        notes = item.GetProperty("notes").GetString(),
    };

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        string body = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue(body);
        using JsonDocument document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("data").Clone();
    }
}
