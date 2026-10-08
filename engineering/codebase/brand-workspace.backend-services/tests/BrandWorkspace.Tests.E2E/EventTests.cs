using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using BrandWorkspace.Application.Access;
using BrandWorkspace.Application.Preparation.Models;
using BrandWorkspace.Infrastructure.Events.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BrandWorkspace.Tests.E2E;

/// <summary>Exercises the showcase events feed, its summary and bundle import against real PostgreSQL and membership.</summary>
public sealed class EventTests(PreparationDatabaseFixture fixture) : IClassFixture<PreparationDatabaseFixture>
{
    private static readonly CancellationToken Token = CancellationToken.None;

    [Fact]
    public async Task Events_ShouldApplyDefaults_PersistRevisions_AndDelete()
    {
        OrganizationAccess owner = await fixture.CreateOwnerAsync();
        using HttpClient client = await fixture.LoginAsync(owner);
        string path = Path(owner);

        JsonElement created = await DataAsync(await client.PostAsJsonAsync(path, new
        {
            title = "  Tashkent Fashion Week ", organizer = "Fashion Council", scope = "local", format = "runway", country = "uz",
            city = "Tashkent", venue = " ", startsOn = "2026-10-20", endsOn = "2026-10-24",
            participation = "Application package to the organiser", cost = "", website = "https://example.test/tfw",
            notes = "Show the shawl line\nBring the catalogue"
        }));
        Guid id = created.GetProperty("id").GetGuid();
        created.EnumerateObject().Select(property => property.Name).Should().Equal(
            "id", "key", "title", "organizer", "scope", "format", "country", "city", "venue", "startsOn", "endsOn", "datePrecision",
            "applicationDeadline", "participation", "cost", "website", "sourceUrl", "status", "priority", "notes", "revision",
            "createdAt", "updatedAt");
        created.GetProperty("key").ValueKind.Should().Be(JsonValueKind.Null);
        created.GetProperty("title").GetString().Should().Be("Tashkent Fashion Week");
        created.GetProperty("country").GetString().Should().Be("UZ");
        created.GetProperty("venue").ValueKind.Should().Be(JsonValueKind.Null);
        created.GetProperty("cost").ValueKind.Should().Be(JsonValueKind.Null);
        created.GetProperty("startsOn").GetString().Should().Be("2026-10-20");
        created.GetProperty("endsOn").GetString().Should().Be("2026-10-24");
        created.GetProperty("datePrecision").GetString().Should().Be("day");
        created.GetProperty("applicationDeadline").ValueKind.Should().Be(JsonValueKind.Null);
        created.GetProperty("status").GetString().Should().Be("shortlisted");
        created.GetProperty("priority").GetString().Should().Be("normal");
        created.GetProperty("revision").GetInt32().Should().Be(1);

        JsonElement undated = await DataAsync(await client.PostAsJsonAsync(path, new { title = "Somewhere fair", scope = "international", format = "trade-fair" }));
        undated.GetProperty("datePrecision").GetString().Should().Be("unknown");
        undated.GetProperty("startsOn").ValueKind.Should().Be(JsonValueKind.Null);
        undated.GetProperty("participation").GetString().Should().BeEmpty();

        (await DataAsync(await client.GetAsync($"{path}/{id}"))).GetProperty("notes").GetString()
            .Should().Be("Show the shawl line\nBring the catalogue");

        JsonElement updated = await DataAsync(await client.PutAsJsonAsync($"{path}/{id}", new
        {
            expectedRevision = 1, title = "Tashkent Fashion Week", scope = "local", format = "runway", country = "UZ",
            startsOn = "2026-11-01", datePrecision = "month", applicationDeadline = "2026-10-15", cost = "Stand: 1 200 USD",
            status = "applying", priority = "high"
        }));
        updated.GetProperty("revision").GetInt32().Should().Be(2);
        updated.GetProperty("datePrecision").GetString().Should().Be("month");
        updated.GetProperty("endsOn").ValueKind.Should().Be(JsonValueKind.Null);
        updated.GetProperty("applicationDeadline").GetString().Should().Be("2026-10-15");
        updated.GetProperty("status").GetString().Should().Be("applying");
        updated.GetProperty("priority").GetString().Should().Be("high");
        updated.GetProperty("website").ValueKind.Should().Be(JsonValueKind.Null);

        await ProblemAsync(await client.PutAsJsonAsync($"{path}/{id}", new
        {
            expectedRevision = 1, title = "Stale", scope = "local", format = "runway"
        }), HttpStatusCode.Conflict, "conflict");
        await ProblemAsync(await client.DeleteAsync($"{path}/{id}?expectedRevision=1"), HttpStatusCode.Conflict, "conflict");
        (await DataAsync(await client.DeleteAsync($"{path}/{id}?expectedRevision=2"))).GetProperty("id").GetGuid().Should().Be(id);
        await ProblemAsync(await client.GetAsync($"{path}/{id}"), HttpStatusCode.NotFound, "not-found");
        await ProblemAsync(await client.PutAsJsonAsync($"{path}/{id}", new { expectedRevision = 2, title = "Gone", scope = "local", format = "runway" }),
            HttpStatusCode.NotFound, "not-found");
        await ProblemAsync(await client.DeleteAsync($"{path}/{id}?expectedRevision=2"), HttpStatusCode.NotFound, "not-found");
    }

    [Fact]
    public async Task List_ShouldOrderByStartThenTitle_AndFilter()
    {
        OrganizationAccess owner = await fixture.CreateOwnerAsync();
        using HttpClient client = await fixture.LoginAsync(owner);
        string path = Path(owner);
        await CreateAsync(client, path, new { title = "Bravo Market", scope = "local", format = "market", country = "UZ", city = "Tashkent", startsOn = "2026-11-10", status = "candidate" });
        await CreateAsync(client, path, new { title = "Alpha Runway", scope = "regional", format = "runway", country = "kz", city = "Almaty", startsOn = "2026-10-01", endsOn = "2026-10-31" });
        await CreateAsync(client, path, new { title = "Charlie Expo", organizer = "Dubai Design District", scope = "international", format = "exhibition", country = "AE", startsOn = "2026-12-05", endsOn = "2026-12-07" });
        await CreateAsync(client, path, new { title = "100% Uzbek Design", scope = "local", format = "festival" });
        await CreateAsync(client, path, new { title = "aardvark fair", scope = "local", format = "trade-fair", startsOn = "2026-10-01" });

        (await TitlesAsync(client, path)).Should().Equal("aardvark fair", "Alpha Runway", "Bravo Market", "Charlie Expo", "100% Uzbek Design");
        (await TitlesAsync(client, $"{path}?scope=regional")).Should().Equal("Alpha Runway");
        (await TitlesAsync(client, $"{path}?format=exhibition")).Should().Equal("Charlie Expo");
        (await TitlesAsync(client, $"{path}?status=candidate")).Should().Equal("Bravo Market");
        (await TitlesAsync(client, $"{path}?query=ALMATY")).Should().Equal("Alpha Runway");
        (await TitlesAsync(client, $"{path}?query=kz")).Should().Equal("Alpha Runway");
        (await TitlesAsync(client, $"{path}?query=design%20district")).Should().Equal("Charlie Expo");
        (await TitlesAsync(client, $"{path}?query=%25")).Should().Equal("100% Uzbek Design");
        (await TitlesAsync(client, $"{path}?query=_")).Should().BeEmpty();

        (await TitlesAsync(client, $"{path}?from=2026-10-15&to=2026-11-30")).Should().Equal("Alpha Runway", "Bravo Market", "100% Uzbek Design");
        (await TitlesAsync(client, $"{path}?from=2026-10-15&to=2026-11-30&includeUndated=false")).Should().Equal("Alpha Runway", "Bravo Market");
        (await TitlesAsync(client, $"{path}?from=2026-12-07")).Should().Equal("Charlie Expo", "100% Uzbek Design");
        (await TitlesAsync(client, $"{path}?to=2026-10-01&includeUndated=false")).Should().Equal("aardvark fair", "Alpha Runway");
        (await TitlesAsync(client, $"{path}?includeUndated=false")).Should().Equal("aardvark fair", "Alpha Runway", "Bravo Market", "Charlie Expo");
        (await TitlesAsync(client, $"{path}?limit=2&offset=1")).Should().Equal("Alpha Runway", "Bravo Market");

        await ProblemAsync(await client.GetAsync($"{path}?scope=galactic"), HttpStatusCode.BadRequest, "validation");
        await ProblemAsync(await client.GetAsync($"{path}?status=won"), HttpStatusCode.BadRequest, "validation");
        await ProblemAsync(await client.GetAsync($"{path}?from=2026-13-01"), HttpStatusCode.BadRequest, "validation");
        await ProblemAsync(await client.GetAsync($"{path}?limit=501"), HttpStatusCode.BadRequest, "validation");
    }

    [Fact]
    public async Task List_ShouldKeepMonthDatedEventsUntilTheirMonthEnds()
    {
        OrganizationAccess owner = await fixture.CreateOwnerAsync();
        using HttpClient client = await fixture.LoginAsync(owner);
        string path = Path(owner);
        await CreateAsync(client, path, new { title = "Announced month", scope = "regional", format = "runway", startsOn = "2026-11-01", datePrecision = "month" });
        await CreateAsync(client, path, new { title = "Expected month", scope = "local", format = "market", startsOn = "2026-11-01", datePrecision = "estimated" });
        await CreateAsync(client, path, new { title = "Single day", scope = "local", format = "market", startsOn = "2026-11-01" });

        (await TitlesAsync(client, $"{path}?from=2026-11-30")).Should().Equal("Announced month", "Expected month");
        (await TitlesAsync(client, $"{path}?from=2026-12-01")).Should().BeEmpty();
    }

    [Fact]
    public async Task Summary_ShouldCountEveryStatus_AndListOpenDeadlinesSoonestFirst()
    {
        OrganizationAccess owner = await fixture.CreateOwnerAsync();
        using HttpClient client = await fixture.LoginAsync(owner);
        string path = Path(owner);

        JsonElement empty = await DataAsync(await client.GetAsync($"{path}/summary"));
        empty.GetProperty("byStatus").EnumerateObject().Select(property => property.Name).Should().Equal(EventValidationService.Statuses);
        empty.GetProperty("byStatus").EnumerateObject().Should().OnlyContain(property => property.Value.GetInt32() == 0);
        empty.GetProperty("upcomingDeadlines").GetArrayLength().Should().Be(0);

        await CreateAsync(client, path, Deadline("Closed yesterday", "2026-10-07", "shortlisted"));
        await CreateAsync(client, path, Deadline("Closes today", "2026-10-08", "candidate"));
        await CreateAsync(client, path, Deadline("Skipped", "2026-10-09", "skipped"));
        await CreateAsync(client, path, Deadline("Rejected", "2026-10-09", "rejected"));
        await CreateAsync(client, path, Deadline("In two days", "2026-10-10", "applying"));
        await CreateAsync(client, path, Deadline("November", "2026-11-01", "applied"));
        await CreateAsync(client, path, Deadline("Mid October", "2026-10-15", "shortlisted"));
        await CreateAsync(client, path, Deadline("December", "2026-12-01", "confirmed"));
        await CreateAsync(client, path, Deadline("Next year", "2027-01-01", "attended"));
        await CreateAsync(client, path, new { title = "No deadline", scope = "local", format = "market" });

        JsonElement summary = await DataAsync(await client.GetAsync($"{path}/summary?today=2026-10-08"));
        JsonElement byStatus = summary.GetProperty("byStatus");
        byStatus.EnumerateObject().Select(property => property.Name).Should().Equal(EventValidationService.Statuses);
        byStatus.EnumerateObject().Select(property => property.Value.GetInt32()).Should().Equal(1, 3, 1, 1, 1, 1, 1, 1);
        summary.GetProperty("upcomingDeadlines").EnumerateArray().Select(item => item.GetProperty("title").GetString())
            .Should().Equal("Closes today", "In two days", "Mid October", "November", "December");
        summary.GetProperty("upcomingDeadlines")[0].GetProperty("applicationDeadline").GetString().Should().Be("2026-10-08");

        await ProblemAsync(await client.GetAsync($"{path}/summary?today=tomorrow"), HttpStatusCode.BadRequest, "validation");
    }

    [Theory]
    [InlineData("""{"title":"Bad scope","scope":"galactic","format":"runway"}""")]
    [InlineData("""{"title":"Bad format","scope":"local","format":"gala"}""")]
    [InlineData("""{"title":"Bad status","scope":"local","format":"runway","status":"won"}""")]
    [InlineData("""{"title":" ","scope":"local","format":"runway"}""")]
    [InlineData("""{"title":"Backwards","scope":"local","format":"runway","startsOn":"2026-10-10","endsOn":"2026-10-09"}""")]
    [InlineData("""{"title":"End only","scope":"local","format":"runway","endsOn":"2026-10-09"}""")]
    [InlineData("""{"title":"Unknown dated","scope":"local","format":"runway","startsOn":"2026-10-10","datePrecision":"unknown"}""")]
    [InlineData("""{"title":"Mid month","scope":"local","format":"runway","startsOn":"2026-10-15","datePrecision":"month"}""")]
    [InlineData("""{"title":"Estimated undated","scope":"local","format":"runway","datePrecision":"estimated"}""")]
    [InlineData("""{"title":"Day undated","scope":"local","format":"runway","datePrecision":"day"}""")]
    [InlineData("""{"title":"No such day","scope":"local","format":"runway","startsOn":"2026-02-30"}""")]
    [InlineData("""{"title":"Too early","scope":"local","format":"runway","applicationDeadline":"1999-12-31"}""")]
    [InlineData("""{"title":"Script","scope":"local","format":"runway","website":"javascript:alert(1)"}""")]
    [InlineData("""{"title":"Script source","scope":"local","format":"runway","sourceUrl":"ftp://example.test/list"}""")]
    [InlineData("""{"title":"Country","scope":"local","format":"runway","country":"UZB"}""")]
    public async Task Create_ShouldRejectUnusableEvents(string body)
    {
        OrganizationAccess owner = await fixture.CreateOwnerAsync();
        using HttpClient client = await fixture.LoginAsync(owner);
        using var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        await ProblemAsync(await client.PostAsync(Path(owner), content), HttpStatusCode.BadRequest, "validation");
        (await DataAsync(await client.GetAsync(Path(owner)))).GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Membership_ShouldHideForeignEvents_AndKeepViewersReadOnly()
    {
        OrganizationAccess owner = await fixture.CreateOwnerAsync();
        OrganizationAccess stranger = await fixture.CreateOwnerAsync();
        using HttpClient client = await fixture.LoginAsync(owner);
        using HttpClient strangerClient = await fixture.LoginAsync(stranger);
        Guid id = (await DataAsync(await client.PostAsJsonAsync(Path(owner), new { title = "Private", scope = "local", format = "market" })))
            .GetProperty("id").GetGuid();

        (await strangerClient.GetAsync(Path(owner))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await strangerClient.GetAsync($"{Path(owner)}/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ProblemAsync(await strangerClient.GetAsync($"{Path(stranger)}/{id}"), HttpStatusCode.NotFound, "not-found");
        await ProblemAsync(await strangerClient.PutAsJsonAsync($"{Path(stranger)}/{id}", new
        {
            expectedRevision = 1, title = "Taken", scope = "local", format = "market"
        }), HttpStatusCode.NotFound, "not-found");
        await ProblemAsync(await strangerClient.DeleteAsync($"{Path(stranger)}/{id}?expectedRevision=1"), HttpStatusCode.NotFound, "not-found");
        (await DataAsync(await strangerClient.GetAsync(Path(stranger)))).GetArrayLength().Should().Be(0);
        (await DataAsync(await strangerClient.GetAsync($"{Path(stranger)}/summary"))).GetProperty("byStatus")
            .GetProperty("shortlisted").GetInt32().Should().Be(0);

        await fixture.SetRoleAsync(owner, "viewer");
        (await DataAsync(await client.GetAsync($"{Path(owner)}/{id}"))).GetProperty("title").GetString().Should().Be("Private");
        (await DataAsync(await client.GetAsync($"{Path(owner)}/summary"))).GetProperty("byStatus")
            .GetProperty("shortlisted").GetInt32().Should().Be(1);
        await ProblemAsync(await client.PostAsJsonAsync(Path(owner), new { title = "Blocked", scope = "local", format = "market" }),
            HttpStatusCode.Forbidden, "forbidden");
        await ProblemAsync(await client.PutAsJsonAsync($"{Path(owner)}/{id}", new
        {
            expectedRevision = 1, title = "Blocked", scope = "local", format = "market"
        }), HttpStatusCode.Forbidden, "forbidden");
        await ProblemAsync(await client.DeleteAsync($"{Path(owner)}/{id}?expectedRevision=1"), HttpStatusCode.Forbidden, "forbidden");
    }

    [Fact]
    public async Task Import_ShouldValidateFirst_BeIdempotent_AndNeverOverwriteDeskEdits()
    {
        OrganizationAccess owner = await fixture.CreateOwnerAsync();
        using HttpClient client = await fixture.LoginAsync(owner);
        string path = Path(owner);
        string directory = Directory.CreateTempSubdirectory("events-import-").FullName;
        try
        {
            string bundle = await WriteBundleAsync(directory, "bundle.json", ValidEvents());

            EventImportReport dryRun = await ImportAsync(owner, bundle, dryRun: true);
            dryRun.DryRun.Should().BeTrue();
            dryRun.Source.Should().Be("synthetic fixture");
            dryRun.Created.Should().Be(3);
            dryRun.Existing.Should().Be(0);
            dryRun.Failures.Should().BeEmpty();
            (await DataAsync(await client.GetAsync(path))).GetArrayLength().Should().Be(0);

            EventImportReport imported = await ImportAsync(owner, bundle, dryRun: false);
            imported.Created.Should().Be(3);
            imported.Existing.Should().Be(0);
            JsonElement events = await DataAsync(await client.GetAsync(path));
            events.EnumerateArray().Select(item => item.GetProperty("key").GetString()).Should().Equal("tfw-2026", "cafw-almaty", "dubai-design-week");
            JsonElement tfw = events[0];
            tfw.GetProperty("status").GetString().Should().Be("candidate");
            tfw.GetProperty("datePrecision").GetString().Should().Be("day");
            tfw.GetProperty("country").GetString().Should().Be("UZ");
            events[1].GetProperty("status").GetString().Should().Be("shortlisted");
            events[2].GetProperty("datePrecision").GetString().Should().Be("estimated");
            events[2].GetProperty("cost").ValueKind.Should().Be(JsonValueKind.Null);
            (await TitlesAsync(client, $"{path}?query=cafw")).Should().Equal("Central Asia Fashion Week");

            Guid tfwId = tfw.GetProperty("id").GetGuid();
            await DataAsync(await client.PutAsJsonAsync($"{path}/{tfwId}", new
            {
                expectedRevision = 1, title = "Tashkent Fashion Week (edited in Desk)", scope = "local", format = "runway",
                startsOn = "2026-10-20", status = "applying", notes = "Application sent by hand."
            }));
            EventImportReport repeated = await ImportAsync(owner, bundle, dryRun: false);
            repeated.Created.Should().Be(0);
            repeated.Existing.Should().Be(3);
            JsonElement kept = await DataAsync(await client.GetAsync($"{path}/{tfwId}"));
            kept.GetProperty("title").GetString().Should().Be("Tashkent Fashion Week (edited in Desk)");
            kept.GetProperty("status").GetString().Should().Be("applying");
            kept.GetProperty("notes").GetString().Should().Be("Application sent by hand.");
            kept.GetProperty("revision").GetInt32().Should().Be(2);

            object[] invalidEvents =
            [
                new { key = "new-event", title = "Would be new", scope = "local", format = "market" },
                new { key = "backwards", title = "Backwards", scope = "local", format = "market", startsOn = "2026-10-10", endsOn = "2026-10-01" },
                new { key = "new-event", title = "Repeated", scope = "local", format = "market" },
                new { title = "No key", scope = "local", format = "market" }
            ];
            string invalid = await WriteBundleAsync(directory, "invalid.json", invalidEvents);
            Func<Task> rejected = () => ImportAsync(owner, invalid, dryRun: false);
            string message = (await rejected.Should().ThrowAsync<PreparationException>()).Which.Message;
            message.Should().Contain("nothing was imported").And.Contain("events[1] backwards").And.Contain("repeated").And.Contain("key is required");
            (await DataAsync(await client.GetAsync(path))).GetArrayLength().Should().Be(3);

            string wrongFormat = System.IO.Path.Combine(directory, "wrong.json");
            await File.WriteAllTextAsync(wrongFormat, """{"format":"ocharo.sourcing-import.v1","events":[]}""", Token);
            Func<Task> wrong = () => ImportAsync(owner, wrongFormat, dryRun: false);
            (await wrong.Should().ThrowAsync<PreparationException>()).Which.Message.Should().Contain(EventImportService.Format);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static object[] ValidEvents() =>
    [
        new
        {
            key = "tfw-2026", title = "Tashkent Fashion Week", organizer = "Synthetic Council", scope = "local", format = "runway",
            country = "uz", city = "Tashkent", startsOn = "2026-10-20", endsOn = "2026-10-24",
            participation = "Application package to the organiser", website = "https://example.test/tfw",
            sourceUrl = "https://example.test/news/tfw", notes = "Synthetic fixture."
        },
        new
        {
            key = "cafw-almaty", title = "Central Asia Fashion Week", scope = "regional", format = "runway", country = "KZ",
            city = "Almaty", startsOn = "2026-10-28", datePrecision = "day", status = "shortlisted", priority = "high"
        },
        new
        {
            key = "dubai-design-week", title = "Dubai Design Week", scope = "international", format = "exhibition", country = "AE",
            startsOn = "2026-11-01", datePrecision = "estimated", applicationDeadline = "2026-09-01", cost = " "
        }
    ];

    private async Task<EventImportReport> ImportAsync(OrganizationAccess owner, string bundle, bool dryRun)
    {
        using IServiceScope scope = fixture.Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<EventImportService>().ImportAsync(owner, bundle, dryRun, Token);
    }

    private static async Task<string> WriteBundleAsync(string directory, string name, object[] events)
    {
        string file = System.IO.Path.Combine(directory, name);
        await File.WriteAllTextAsync(file, JsonSerializer.Serialize(new
        {
            format = EventImportService.Format,
            source = "synthetic fixture",
            events
        }), Token);
        return file;
    }

    private static object Deadline(string title, string deadline, string status) =>
        new { title, scope = "local", format = "market", applicationDeadline = deadline, status };

    private static async Task CreateAsync(HttpClient client, string path, object body) =>
        await DataAsync(await client.PostAsJsonAsync(path, body));

    private static async Task<IReadOnlyList<string?>> TitlesAsync(HttpClient client, string url) =>
        (await DataAsync(await client.GetAsync(url))).EnumerateArray().Select(item => item.GetProperty("title").GetString()).ToArray();

    private static string Path(OrganizationAccess access) => $"/api/organizations/{access.OrganizationId}/events";

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        using (response)
        {
            string body = await response.Content.ReadAsStringAsync();
            response.StatusCode.Should().Be(HttpStatusCode.OK, body);
            return JsonDocument.Parse(body).RootElement.GetProperty("data").Clone();
        }
    }

    private static async Task ProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        using (response)
        {
            string body = await response.Content.ReadAsStringAsync();
            response.StatusCode.Should().Be(status, body);
            JsonDocument.Parse(body).RootElement.GetProperty("code").GetString().Should().Be(code);
        }
    }
}
