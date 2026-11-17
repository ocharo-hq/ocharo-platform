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
    public async Task TrackingRoute_ShouldSaveTheStage_AfterTheSourceRevisedTheFactsSinceThePageRead()
    {
        var service = new FakeEventsService();
        EventModel sourced = service.Add(new EventFacts
        {
            Title = new Dictionary<string, string> { ["und"] = "Tashkent Fashion Week" },
            Kind = "runway",
            Place = new EventPlace { City = "Tashkent", Country = "UZ" },
            Starts = new EventWhen { Value = "2026-12-03", Precision = DatePrecision.Day },
        });
        await using var host = EventsHost.Create(new Dictionary<string, string?>(), service);
        using HttpClient client = await EventsTestIdentity.SignedIn(host.Factory, EventsTestIdentity.Editor);
        JsonElement listed = (await DataAsync(await client.GetAsync(Path))).EnumerateArray().Single();
        listed.GetProperty("eventRevision").GetInt32().Should().Be(1);
        service.Revise(sourced.Id, facts => facts with
        {
            Starts = new EventWhen { Value = "2026-12-04", Precision = DatePrecision.Day },
        });

        JsonElement shortlisted = await DataAsync(await client.PutAsJsonAsync($"{Path}/{sourced.Id}/tracking", new
        {
            expectedRevision = 1, status = "shortlisted", priority = "high", participation = "Booth", cost = "USD 500",
            notes = "Fits.",
        }));

        shortlisted.GetProperty("status").GetString().Should().Be("shortlisted");
        shortlisted.GetProperty("priority").GetString().Should().Be("high");
        shortlisted.GetProperty("startsOn").GetString().Should().Be("2026-12-04");
        shortlisted.GetProperty("eventRevision").GetInt32().Should().Be(2);
        shortlisted.GetProperty("revision").GetInt32().Should().Be(2);
        service.TrackingWrites.Should().ContainSingle().Which.Stage.Should().Be("shortlisted");

        // A full replacement from the stale page answers 409 (its facts revision moved), not 410.
        (await client.PutAsJsonAsync($"{Path}/{sourced.Id}", Body(listed, 2, status: "applying", eventRevision: 1)))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        // A real fact change on a current page still answers 410.
        (await client.PutAsJsonAsync($"{Path}/{sourced.Id}", Body(shortlisted, 2, title: "Renamed", eventRevision: 2)))
            .StatusCode.Should().Be(HttpStatusCode.Gone);
        // A stale tracking revision answers 409, and an unknown stage 400.
        (await client.PutAsJsonAsync($"{Path}/{sourced.Id}/tracking", new { expectedRevision = 1, status = "applying" }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await client.PutAsJsonAsync($"{Path}/{sourced.Id}/tracking", new { expectedRevision = 2, status = "registered" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        service.TrackingWrites.Should().ContainSingle();
    }

    [Fact]
    public async Task EventsPage_ShouldShowTheTitleInTheLanguageTheRequestAsksFor()
    {
        var service = new FakeEventsService();
        EventModel sourced = service.Add(new EventFacts
        {
            Title = new Dictionary<string, string> { ["ru-Cyrl"] = "Неделя моды", ["uz-Latn"] = "Moda haftaligi" },
            Languages = ["ru-Cyrl"],
            Kind = "runway",
            Place = new EventPlace { City = "Tashkent", Country = "UZ" },
            Starts = new EventWhen { Value = "2026-12-03", Precision = DatePrecision.Day },
        });
        await using var host = EventsHost.Create(new Dictionary<string, string?>(), service);
        using HttpClient client = await EventsTestIdentity.SignedIn(host.Factory, EventsTestIdentity.Owner);

        async Task<JsonElement> ReadAsync(string url, string? language)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (language is not null) request.Headers.TryAddWithoutValidation("Accept-Language", language);
            return await DataAsync(await client.SendAsync(request));
        }

        JsonElement uz = (await ReadAsync(Path, "uz,ru;q=0.5")).EnumerateArray().Single();
        JsonElement ru = await ReadAsync($"{Path}/{sourced.Id}", "ru");

        uz.GetProperty("title").GetString().Should().Be("Moda haftaligi");
        uz.GetProperty("titleLanguage").GetString().Should().Be("uz-Latn");
        uz.GetProperty("languages").EnumerateArray().Select(language => language.GetString()).Should().Equal("ru-Cyrl");
        ru.GetProperty("title").GetString().Should().Be("Неделя моды");
        ru.GetProperty("titleLanguage").GetString().Should().Be("ru-Cyrl");
    }

    [Fact]
    public async Task EventsPage_ShouldShowACancelledOnlineEventAsSuch()
    {
        var service = new FakeEventsService();
        service.Add(new EventFacts
        {
            Title = new Dictionary<string, string> { ["und"] = "Online craft fair" },
            Kind = "market",
            Status = EventStatus.Cancelled,
            Attendance = EventAttendance.Online,
            Starts = new EventWhen { Value = "2026-12-03", Precision = DatePrecision.Day },
        });
        service.Add(new EventFacts
        {
            Title = new Dictionary<string, string> { ["und"] = "Tashkent Fashion Week" },
            Kind = "runway",
            Starts = new EventWhen { Value = "2026-12-05", Precision = DatePrecision.Day },
        });
        await using var host = EventsHost.Create(new Dictionary<string, string?>(), service);
        using HttpClient client = await EventsTestIdentity.SignedIn(host.Factory, EventsTestIdentity.Viewer);

        JsonElement[] listed = (await DataAsync(await client.GetAsync(Path))).EnumerateArray().ToArray();

        JsonElement cancelled = listed.Single(item => item.GetProperty("title").GetString() == "Online craft fair");
        cancelled.GetProperty("eventStatus").GetString().Should().Be("cancelled");
        cancelled.GetProperty("attendance").GetString().Should().Be("online");
        JsonElement live = listed.Single(item => item.GetProperty("title").GetString() == "Tashkent Fashion Week");
        live.GetProperty("eventStatus").GetString().Should().Be("scheduled");
        live.GetProperty("attendance").GetString().Should().Be("offline");
    }

    [Fact]
    public async Task EventsPage_ShouldShowTicketPricesApartFromOcharosOwnCost()
    {
        var service = new FakeEventsService();
        service.Add(new EventFacts
        {
            Title = new Dictionary<string, string> { ["und"] = "Ticketed fair" },
            Kind = "trade-fair",
            Offers = [new EventOffer { Kind = OfferKind.Ticket, MinPrice = 150000, MaxPrice = 150000, Currency = "UZS" }],
            TicketUrl = "https://tickets.example.test/fair",
            Starts = new EventWhen { Value = "2026-12-03", Precision = DatePrecision.Day },
        });
        service.Add(new EventFacts
        {
            Title = new Dictionary<string, string> { ["und"] = "Plain fair" },
            Kind = "trade-fair",
            Starts = new EventWhen { Value = "2026-12-05", Precision = DatePrecision.Day },
        });
        await using var host = EventsHost.Create(new Dictionary<string, string?>(), service);
        using HttpClient client = await EventsTestIdentity.SignedIn(host.Factory, EventsTestIdentity.Viewer);

        JsonElement[] listed = (await DataAsync(await client.GetAsync(Path))).EnumerateArray().ToArray();

        JsonElement ticketed = listed.Single(item => item.GetProperty("title").GetString() == "Ticketed fair");
        JsonElement tickets = ticketed.GetProperty("tickets");
        tickets.GetProperty("minPrice").GetDecimal().Should().Be(150000);
        tickets.GetProperty("currency").GetString().Should().Be("UZS");
        tickets.GetProperty("free").GetBoolean().Should().BeFalse();
        ticketed.GetProperty("ticketUrl").GetString().Should().Be("https://tickets.example.test/fair");
        IsNullOrAbsent(ticketed, "cost").Should().BeTrue();
        IsNullOrAbsent(listed.Single(item => item.GetProperty("title").GetString() == "Plain fair"), "tickets")
            .Should().BeTrue();
    }

    [Fact]
    public async Task EventsPage_ShouldShowTheStartTimeZoneAddressAndOrganizerSite()
    {
        var service = new FakeEventsService();
        service.Add(new EventFacts
        {
            Title = new Dictionary<string, string> { ["ru-Cyrl"] = "Jazz evening" },
            Kind = "other",
            Organizer = new EventOrganizer { Name = "Jazz Club", Website = "https://jazz.example.test/" },
            Place = new EventPlace { Venue = "Ilkhom Theatre", Address = "Pakhtakor street 5", City = "Tashkent", Country = "UZ" },
            Starts = new EventWhen { Value = "2026-12-05T19:00", Precision = DatePrecision.Minute },
            TimeZone = "Asia/Tashkent",
        });
        await using var host = EventsHost.Create(new Dictionary<string, string?>(), service);
        using HttpClient client = await EventsTestIdentity.SignedIn(host.Factory, EventsTestIdentity.Viewer);

        JsonElement listed = (await DataAsync(await client.GetAsync(Path))).EnumerateArray().Single();
        JsonElement read = await DataAsync(await client.GetAsync($"{Path}/{listed.GetProperty("id").GetGuid()}"));

        foreach (JsonElement item in new[] { listed, read })
        {
            item.GetProperty("startsOn").GetString().Should().Be("2026-12-05");
            item.GetProperty("startsAt").GetString().Should().Be("19:00");
            item.GetProperty("timeZone").GetString().Should().Be("Asia/Tashkent");
            item.GetProperty("address").GetString().Should().Be("Pakhtakor street 5");
            item.GetProperty("organizerWebsite").GetString().Should().Be("https://jazz.example.test/");
        }
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

    /// <summary>The wire leaves a null member out; either form reads as no value.</summary>
    private static bool IsNullOrAbsent(JsonElement item, string name) =>
        !item.TryGetProperty(name, out JsonElement value) || value.ValueKind == JsonValueKind.Null;

    private static object Body(
        JsonElement item, int expectedRevision, string? status = null, string? title = null, int? eventRevision = null) => new
    {
        expectedRevision,
        eventRevision,
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
