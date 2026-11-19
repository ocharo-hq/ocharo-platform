using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OcharoEvents.Infrastructure.Delivery.Services;
using OcharoEvents.Infrastructure.Events.Services;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Client;
using WoW.Two.Sdk.Beta.Be.Events.Digests;
using WoW.Two.Sdk.Beta.Be.Events.Intake;
using WoW.Two.Sdk.Beta.Be.Events.Tracking;
using WoW.Two.Sdk.Beta.Be.Events.Workflows;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Http.Auth.OAuth2TokenExchange;

namespace OcharoEvents.Tests.E2E;

/// <summary>
/// The Events page's mirror of WoW2 Events (OE07): the <c>fe-events</c> kit's tracking, event, proposal, workflow and
/// calendar routes under <c>/api/organizations/{id}/events-api</c>. A list is one Events call with the kit's filters and
/// cursor; writes pass their spec as sent; a person without the permission, another organization and another catalog
/// never reach Events.
/// </summary>
public sealed class EventsApiRouteTests
{
    private static readonly string Base = $"/api/organizations/{EventsTestIdentity.Organization}/events-api/api/events/v1";
    private static readonly string Catalog = $"{Base}/catalogs/ocharo-fashion";
    private static readonly Guid FashionWeek = Guid.Parse("22222222-2222-4222-8222-222222222222");

    [Fact]
    public async Task TrackingList_ShouldMakeOneEventsCall_WithTheKitsServerSideFiltersAndCursor()
    {
        var events = new RecordingEventsBrokers
        {
            Page = new TrackingPage(
                [new TrackedEventModel
                {
                    Event = RecordingEventsBrokers.Event(FashionWeek, "Tashkent Fashion Week"),
                    Tracking = RecordingEventsBrokers.Tracking(FashionWeek, "shortlisted", 4),
                }],
                "next-page"),
        };
        await using var host = EventsHost.Create(new Dictionary<string, string?>(), events);
        using HttpClient viewer = await EventsTestIdentity.SignedIn(host.Factory, EventsTestIdentity.Viewer);

        JsonElement page = await DataAsync(await viewer.GetAsync(
            $"{Catalog}/tracking?excludeStages=skipped&excludeStages=rejected&countries=KZ&countries=KG&kind=runway"
            + "&text=silk&from=2026-11-19&includeUndated=true&cursor=page-2&limit=50"));

        var call = events.Calls.Should().ContainSingle().Which;
        call.Method.Should().Be("ListAsync");
        call.Catalog.Should().Be("ocharo-fashion");
        var query = call.Argument.Should().BeOfType<TrackingQuery>().Which;
        query.ExcludeStages.Should().Equal("skipped", "rejected");
        query.Stages.Should().BeEmpty();
        query.Countries.Should().Equal("KZ", "KG");
        query.ExcludeCountries.Should().BeEmpty();
        query.IncludeUnknownCountry.Should().BeFalse();
        query.Kind.Should().Be("runway");
        query.Text.Should().Be("silk");
        query.From.Should().Be(new DateOnly(2026, 11, 19));
        query.To.Should().BeNull();
        query.IncludeUndated.Should().BeTrue();
        query.TrackedOnly.Should().BeFalse();
        query.Cursor.Should().Be("page-2");
        query.Limit.Should().Be(50);
        page.GetProperty("nextCursor").GetString().Should().Be("next-page");
        JsonElement item = page.GetProperty("items").EnumerateArray().Single();
        item.GetProperty("event").GetProperty("id").GetGuid().Should().Be(FashionWeek);
        item.GetProperty("event").GetProperty("facts").GetProperty("title").GetProperty("und").GetString()
            .Should().Be("Tashkent Fashion Week");
        item.GetProperty("tracking").GetProperty("stage").GetString().Should().Be("shortlisted");
        item.GetProperty("tracking").GetProperty("revision").GetInt32().Should().Be(4);
    }

    [Fact]
    public async Task TrackingList_ShouldPassThePastEventsStripsStagesAndLastDay()
    {
        var events = new RecordingEventsBrokers();
        await using var host = EventsHost.Create(new Dictionary<string, string?>(), events);
        using HttpClient editor = await EventsTestIdentity.SignedIn(host.Factory, EventsTestIdentity.Editor);

        (await editor.GetAsync($"{Catalog}/tracking?stages=shortlisted&stages=applying&stages=applied&stages=confirmed"
            + "&to=2026-11-18&includeUndated=false&limit=50&excludeCountries=UZ&includeUnknownCountry=false&priority=high"))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var query = events.Of("ListAsync").Should().ContainSingle().Which.Argument.Should().BeOfType<TrackingQuery>().Which;
        query.Stages.Should().Equal("shortlisted", "applying", "applied", "confirmed");
        query.To.Should().Be(new DateOnly(2026, 11, 18));
        query.IncludeUndated.Should().BeFalse();
        query.ExcludeCountries.Should().Equal("UZ");
        query.Priority.Should().Be(TrackingPriority.High);
    }

    [Fact]
    public async Task Summary_ShouldPassTheDayAndTheDeadlineKinds()
    {
        var events = new RecordingEventsBrokers();
        await using var host = EventsHost.Create(new Dictionary<string, string?>(), events);
        using HttpClient viewer = await EventsTestIdentity.SignedIn(host.Factory, EventsTestIdentity.Viewer);

        JsonElement summary = await DataAsync(await viewer.GetAsync(
            $"{Catalog}/tracking/summary?today=2026-11-19&deadlineKinds=application"));

        var query = events.Of("GetSummaryAsync").Should().ContainSingle().Which.Argument
            .Should().BeOfType<TrackingSummaryQuery>().Which;
        query.Today.Should().Be(new DateOnly(2026, 11, 19));
        query.DeadlineKinds.Should().Equal(DeadlineKind.Application);
        summary.GetProperty("untracked").GetInt32().Should().Be(64);
        summary.GetProperty("byStage").GetProperty("shortlisted").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task Editor_ShouldTrack_ReadHistory_StopTracking_AndProposeThroughEvents()
    {
        var events = new RecordingEventsBrokers();
        await using var host = EventsHost.Create(new Dictionary<string, string?>(), events);
        using HttpClient editor = await EventsTestIdentity.SignedIn(host.Factory, EventsTestIdentity.Editor);

        JsonElement tracked = await DataAsync(await editor.PutAsJsonAsync($"{Catalog}/tracking/{FashionWeek}", new
        {
            stage = "attended",
            priority = "high",
            notes = "Bring the lookbook.",
            fields = new { cost = "Stand from 300 USD", participation = "Application package" },
            references = Array.Empty<object>(),
            expectedRevision = 3,
        }));
        var spec = events.Of("PutAsync").Should().ContainSingle().Which.Argument.Should().BeOfType<TrackingSpec>().Which;
        spec.Stage.Should().Be("attended");
        spec.Priority.Should().Be(TrackingPriority.High);
        spec.Notes.Should().Be("Bring the lookbook.");
        spec.ExpectedRevision.Should().Be(3);
        spec.Fields!.Value.GetProperty("cost").GetString().Should().Be("Stand from 300 USD");
        tracked.GetProperty("stage").GetString().Should().Be("attended");
        tracked.GetProperty("revision").GetInt32().Should().Be(4);

        (await DataAsync(await editor.GetAsync($"{Catalog}/tracking/{FashionWeek}")))
            .GetProperty("event").GetProperty("id").GetGuid().Should().Be(FashionWeek);
        (await DataAsync(await editor.GetAsync($"{Catalog}/tracking/{FashionWeek}/history")))
            .EnumerateArray().Single().GetProperty("toStage").GetString().Should().Be("shortlisted");
        (await DataAsync(await editor.GetAsync($"{Catalog}/events/{FashionWeek}")))
            .GetProperty("review").GetString().Should().Be("published");
        (await editor.DeleteAsync($"{Catalog}/tracking/{FashionWeek}?expectedRevision=4")).StatusCode
            .Should().Be(HttpStatusCode.OK);
        events.Of("DeleteAsync").Should().ContainSingle().Which.Argument.Should().Be(4);
        (await editor.DeleteAsync($"{Catalog}/tracking/{FashionWeek}")).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        HttpResponseMessage proposed = await editor.PostAsJsonAsync($"{Catalog}/candidates", new
        {
            facts = new
            {
                title = new Dictionary<string, string> { ["und"] = "Margilan Ikat Days" },
                kind = "festival",
                place = new { country = "UZ" },
            },
        });
        proposed.StatusCode.Should().Be(HttpStatusCode.Created);
        (await DataAsync(proposed)).GetProperty("facts").GetProperty("title").GetProperty("und").GetString()
            .Should().Be("Margilan Ikat Days");
        events.Of("ProposeAsync").Should().ContainSingle().Which.Argument.Should().BeOfType<EventSpec>()
            .Which.Facts.Kind.Should().Be("festival");
    }

    [Fact]
    public async Task Owner_ShouldReadAndWriteTheWorkflow_AndExportTheCalendar()
    {
        var events = new RecordingEventsBrokers();
        await using var host = EventsHost.Create(new Dictionary<string, string?>(), events);
        using HttpClient owner = await EventsTestIdentity.SignedIn(host.Factory, EventsTestIdentity.Owner);

        JsonElement workflow = await DataAsync(await owner.GetAsync($"{Base}/workflow"));
        workflow.GetProperty("defaultStage").GetString().Should().Be("candidate");
        workflow.GetProperty("revision").GetInt32().Should().Be(3);

        JsonElement written = await DataAsync(await owner.PutAsJsonAsync($"{Base}/workflow", new
        {
            stages = new object[]
            {
                new { key = "candidate", label = new { en = "Candidate", uz = "Nomzod", ru = "Кандидат" }, @class = "open" },
                new { key = "skipped", label = new { en = "Skipped" }, @class = "lost" },
            },
            defaultStage = "candidate",
            fields = new { type = "object", properties = new { sourceUrl = new { type = "string", readOnly = true } } },
            expectedRevision = 3,
        }));
        var spec = events.Of("PutWorkflowAsync").Should().ContainSingle().Which.Argument.Should().BeOfType<WorkflowSpec>().Which;
        spec.Stages.Select(stage => stage.Class).Should().Equal(StageClass.Open, StageClass.Lost);
        spec.Stages[0].Label["uz"].Should().Be("Nomzod");
        spec.ExpectedRevision.Should().Be(3);
        written.GetProperty("revision").GetInt32().Should().Be(4);

        HttpResponseMessage calendar = await owner.GetAsync($"{Catalog}/events.ics?review=published&from=2026-11-19&kind=runway");
        calendar.StatusCode.Should().Be(HttpStatusCode.OK);
        calendar.Content.Headers.ContentType!.MediaType.Should().Be("text/calendar");
        (await calendar.Content.ReadAsStringAsync()).Should().StartWith("BEGIN:VCALENDAR");
        var query = events.Of("ExportAsync").Should().ContainSingle().Which.Argument.Should().BeOfType<EventQuery>().Which;
        query.Review.Should().Be(EventReviewState.Published);
        query.From.Should().Be(new DateOnly(2026, 11, 19));
        query.Kind.Should().Be("runway");
    }

    [Fact]
    public async Task Writes_ShouldRefuseAViewer_AndTheWorkflowAnEditor_BeforeEvents()
    {
        var events = new RecordingEventsBrokers();
        await using var host = EventsHost.Create(new Dictionary<string, string?>(), events);
        using HttpClient viewer = await EventsTestIdentity.SignedIn(host.Factory, EventsTestIdentity.Viewer);
        using HttpClient editor = await EventsTestIdentity.SignedIn(host.Factory, EventsTestIdentity.Editor);

        (await viewer.PutAsJsonAsync($"{Catalog}/tracking/{FashionWeek}", new { stage = "shortlisted" })).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
        (await viewer.DeleteAsync($"{Catalog}/tracking/{FashionWeek}?expectedRevision=1")).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
        (await viewer.PostAsJsonAsync($"{Catalog}/candidates", new
        {
            facts = new { title = new Dictionary<string, string> { ["und"] = "Margilan Ikat Days" }, kind = "festival" },
        })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await editor.PutAsJsonAsync($"{Base}/workflow", new { stages = Array.Empty<object>(), defaultStage = "candidate" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        events.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task Freshness_ShouldBeReadByAMember_AsOneEventsCall_WithTheCatalogsTimesAndFailingSources()
    {
        var events = new RecordingEventsBrokers
        {
            Freshness = new IntakeFreshnessModel
            {
                LastReceivedAtUtc = RecordingEventsBrokers.At.AddHours(-30),
                NewestObservedAtUtc = RecordingEventsBrokers.At.AddHours(-31),
                FailingSources = 2,
            },
        };
        await using var host = EventsHost.Create(new Dictionary<string, string?>(), events);
        using HttpClient viewer = await EventsTestIdentity.SignedIn(host.Factory, EventsTestIdentity.Viewer);

        JsonElement freshness = await DataAsync(await viewer.GetAsync($"{Catalog}/freshness"));

        var call = events.Calls.Should().ContainSingle().Which;
        call.Method.Should().Be("GetFreshnessAsync");
        call.Catalog.Should().Be("ocharo-fashion");
        freshness.GetProperty("lastReceivedAtUtc").GetDateTimeOffset().Should().Be(RecordingEventsBrokers.At.AddHours(-30));
        freshness.GetProperty("newestObservedAtUtc").GetDateTimeOffset().Should().Be(RecordingEventsBrokers.At.AddHours(-31));
        freshness.GetProperty("failingSources").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task Freshness_ShouldRefuseAnotherCatalog_AnotherOrganization_AMemberWithoutRead_AndNoSession_BeforeEvents()
    {
        var events = new RecordingEventsBrokers();
        await using var host = EventsHost.Create(new Dictionary<string, string?>(), events);
        using HttpClient owner = await EventsTestIdentity.SignedIn(host.Factory, EventsTestIdentity.Owner);
        using HttpClient outsider = await EventsTestIdentity.SignedIn(host.Factory, EventsTestIdentity.Outsider);
        using HttpClient billing = await EventsTestIdentity.SignedIn(host.Factory, EventsTestIdentity.Billing);
        using HttpClient unaffiliated = await EventsTestIdentity.SignedIn(host.Factory, EventsTestIdentity.Unaffiliated);
        using HttpClient anonymous = EventsTestIdentity.Anonymous(host.Factory);
        string other = $"/api/organizations/{EventsTestIdentity.OtherOrganization}/events-api/api/events/v1/catalogs/ocharo-fashion";

        (await owner.GetAsync($"{Base}/catalogs/another-catalog/freshness")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await owner.GetAsync($"{other}/freshness")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await outsider.GetAsync($"{Catalog}/freshness")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await unaffiliated.GetAsync($"{Catalog}/freshness")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await billing.GetAsync($"{Catalog}/freshness")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await anonymous.GetAsync($"{Catalog}/freshness")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        events.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task Freshness_ShouldReachThePage_AsEventsOwn404_WhenEventsRefusesTheMember()
    {
        var events = new RecordingEventsBrokers
        {
            Failure = AppError.Of(AppErrorType.NotFound, "The catalog was not found.", null, "", "", 0),
        };
        await using var host = EventsHost.Create(new Dictionary<string, string?>(), events);
        using HttpClient viewer = await EventsTestIdentity.SignedIn(host.Factory, EventsTestIdentity.Viewer);

        (await viewer.GetAsync($"{Catalog}/freshness")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        events.Of("GetFreshnessAsync").Should().ContainSingle();
    }

    [Fact]
    public async Task Mirror_ShouldRefuseAnotherCatalog_AnotherOrganization_AndMalformedFilters_BeforeEvents()
    {
        var events = new RecordingEventsBrokers();
        await using var host = EventsHost.Create(new Dictionary<string, string?>(), events);
        using HttpClient owner = await EventsTestIdentity.SignedIn(host.Factory, EventsTestIdentity.Owner);
        using HttpClient outsider = await EventsTestIdentity.SignedIn(host.Factory, EventsTestIdentity.Outsider);
        string other = $"/api/organizations/{EventsTestIdentity.OtherOrganization}/events-api/api/events/v1/catalogs/ocharo-fashion";

        (await owner.GetAsync($"{Base}/catalogs/another-catalog/tracking")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await owner.GetAsync($"{Base}/catalogs/another-catalog/events/{FashionWeek}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await owner.GetAsync($"{other}/tracking")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await outsider.GetAsync($"{Catalog}/tracking")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await owner.GetAsync($"{Catalog}/tracking?from=03.12.2026")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await owner.GetAsync($"{Catalog}/tracking?priority=urgent")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await owner.GetAsync($"{Catalog}/tracking/summary?deadlineKinds=someday")).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        events.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task EventsRefusal_ShouldReachThePage_AsItsOwnProblem()
    {
        var events = new RecordingEventsBrokers
        {
            Failure = AppError.Of(AppErrorType.Conflict, "The tracking changed.", null, "", "", 0),
        };
        await using var host = EventsHost.Create(new Dictionary<string, string?>(), events);
        using HttpClient editor = await EventsTestIdentity.SignedIn(host.Factory, EventsTestIdentity.Editor);

        (await editor.PutAsJsonAsync($"{Catalog}/tracking/{FashionWeek}", new { stage = "skipped", expectedRevision = 1 }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Host_ShouldWireEventsByItsOwnIdentityClient_WithNoStoreOfItsOwn()
    {
        await using var host = EventsHost.Create(new Dictionary<string, string?>());
        using var anonymous = EventsTestIdentity.Anonymous(host.Factory);
        (await anonymous.GetAsync("/health")).StatusCode.Should().Be(HttpStatusCode.OK);

        EventsClientOptions client = host.Factory.Services.GetRequiredService<IOptions<EventsClientOptions>>().Value;
        client.BaseAddress.Should().Be(new Uri(EventsHost.EventsUrl));
        client.Identity!.TokenEndpoint.Should().Be(new Uri("https://identity.example.test/connect/token"));
        client.Identity.ClientId.Should().Be("ocharo-events");
        host.Factory.Services.GetRequiredService<IOptions<EventsServiceOptions>>().Value.Catalog.Should().Be("ocharo-fashion");
        host.Factory.Services.GetRequiredService<IOAuth2SubjectTokenSource>().GetType().FullName
            .Should().Be("WoW.Two.Sdk.Beta.Be.Identity.Bff.Sessions.BffSubjectTokenSource");
        using var scope = host.Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<EventsApiService>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<IEventTrackingBroker>().GetType().FullName
            .Should().Be("WoW.Two.Sdk.Beta.Be.Events.Client.Tracking.ApiEventTrackingBroker");
        scope.ServiceProvider.GetRequiredService<IEventWorkflowsBroker>().GetType().FullName
            .Should().Be("WoW.Two.Sdk.Beta.Be.Events.Client.Workflows.ApiEventWorkflowsBroker");
        scope.ServiceProvider.GetRequiredService<IEventIntakeBroker>().GetType().FullName
            .Should().Be("WoW.Two.Sdk.Beta.Be.Events.Client.Intake.ApiEventIntakeBroker");
        // Telegram delivery reaches Events' digests through the same client and person flow.
        scope.ServiceProvider.GetRequiredService<IEventDigestsBroker>().GetType().FullName
            .Should().Be("WoW.Two.Sdk.Beta.Be.Events.Client.Digests.ApiEventDigestsBroker");
        scope.ServiceProvider.GetRequiredService<EventDigestsService>().Should().NotBeNull();
    }

    [Fact]
    public async Task Mirror_ShouldAnswerUnavailable_InALocalRunWithoutEvents()
    {
        await using var host = EventsHost.Create(new Dictionary<string, string?> { ["Events:BaseUrl"] = null }, environment: "Development");
        using HttpClient client = await EventsTestIdentity.SignedIn(host.Factory, EventsTestIdentity.Owner);

        (await client.GetAsync($"{Catalog}/tracking")).StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await client.GetAsync($"{Catalog}/freshness")).StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await client.GetAsync($"{Base}/workflow")).StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        string body = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue(body);
        using JsonDocument document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("data").Clone();
    }
}
