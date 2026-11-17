using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace OcharoEvents.Tests.E2E;

/// <summary>
/// The organization's Telegram delivery (tasks-platform.md O17): WoW2 Events' digest routes, mirrored under the
/// organization for the page's <c>fe-events</c> editor. An admin saves Ocharo's weekly digest, previews it and sends it
/// now; a member without a Telegram link reads <c>not_linked</c>; members without <c>events:admin</c>, another organization
/// and another catalog never reach Events.
/// </summary>
public sealed class TelegramDeliveryRouteTests
{
    private static readonly string Path =
        $"/api/organizations/{EventsTestIdentity.Organization}/delivery/api/events/v1/catalogs/ocharo-fashion/digests";

    [Fact]
    public async Task Admin_ShouldSaveTheWeeklyDigest_PreviewIt_AndSendItNow()
    {
        var digests = new FakeDigestsBroker();
        digests.Linked.Add(EventsTestIdentity.Owner.Subject);
        await using var host = EventsHost.Create(new Dictionary<string, string?>(), digests: digests);
        using HttpClient client = await EventsTestIdentity.SignedIn(host.Factory, EventsTestIdentity.Owner);

        HttpResponseMessage created = await client.PostAsJsonAsync(Path, new
        {
            name = "Ocharo tadbirlari",
            channel = "telegram",
            schedule = new { days = new[] { "monday" }, times = new[] { "09:00" }, timeZone = "Asia/Tashkent" },
            selection = new { newSinceLastRun = true, startingWithinDays = 30, deadlineWithinDays = 14 },
            recipients = new object[] { new { subject = EventsTestIdentity.Owner.Subject }, new { subject = "member-without-telegram" } },
            language = "uz-Latn",
            linkTemplate = "https://events.example.test/organizations/" + EventsTestIdentity.Organization + "/events/{id}",
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        JsonElement digest = await DataAsync(created);
        string id = digest.GetProperty("id").GetString()!;
        digest.GetProperty("schedule").GetProperty("days").EnumerateArray().Single().GetString().Should().Be("monday");
        digest.GetProperty("schedule").GetProperty("timeZone").GetString().Should().Be("Asia/Tashkent");
        digest.GetProperty("selection").GetProperty("startingWithinDays").GetInt32().Should().Be(30);
        digest.GetProperty("selection").GetProperty("deadlineWithinDays").GetInt32().Should().Be(14);
        digest.GetProperty("nextRunAtUtc").GetDateTimeOffset().Should().Be(new DateTimeOffset(2026, 11, 23, 4, 0, 0, TimeSpan.Zero));
        digests.Catalogs.Should().Equal("ocharo-fashion");

        (await DataAsync(await client.GetAsync(Path))).EnumerateArray().Single().GetProperty("id").GetString().Should().Be(id);
        JsonElement preview = await DataAsync(await client.PostAsync($"{Path}/{id}/preview", null));
        preview.GetProperty("texts").EnumerateArray().Single().GetProperty("language").GetString().Should().Be("uz-Latn");

        JsonElement run = await DataAsync(await client.PostAsync($"{Path}/{id}/send", null));
        run.GetProperty("trigger").GetString().Should().Be("manual");
        run.GetProperty("recipients").EnumerateArray()
            .Select(recipient => (recipient.GetProperty("subject").GetString(), recipient.GetProperty("status").GetString()))
            .Should().Equal((EventsTestIdentity.Owner.Subject, "sent"), ("member-without-telegram", "not_linked"));
        JsonElement runs = await DataAsync(await client.GetAsync($"{Path}/{id}/runs?limit=5"));
        runs.GetProperty("items").EnumerateArray().Single().GetProperty("id").GetString()
            .Should().Be(run.GetProperty("id").GetString());

        JsonElement saved = await DataAsync(await client.PutAsJsonAsync($"{Path}/{id}", new
        {
            name = "Ocharo tadbirlari",
            enabled = false,
            schedule = new { days = new[] { "monday", "thursday" }, times = new[] { "09:00" }, timeZone = "Asia/Tashkent" },
            recipients = new object[] { new { subject = EventsTestIdentity.Owner.Subject } },
            expectedRevision = 1,
        }));
        saved.GetProperty("revision").GetInt32().Should().Be(2);
        (await client.PutAsJsonAsync($"{Path}/{id}", new
        {
            name = "Stale", schedule = new { days = new[] { "monday" }, times = new[] { "09:00" }, timeZone = "Asia/Tashkent" },
            recipients = new object[] { new { subject = EventsTestIdentity.Owner.Subject } }, expectedRevision = 1,
        })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await client.DeleteAsync($"{Path}/{id}?expectedRevision=2")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync($"{Path}/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delivery_ShouldRefuseMembersWithoutAdmin_OtherOrganizations_AndOtherCatalogs_BeforeEvents()
    {
        var digests = new FakeDigestsBroker();
        await using var host = EventsHost.Create(new Dictionary<string, string?>(), digests: digests);
        using HttpClient editor = await EventsTestIdentity.SignedIn(host.Factory, EventsTestIdentity.Editor);
        using HttpClient viewer = await EventsTestIdentity.SignedIn(host.Factory, EventsTestIdentity.Viewer);
        using HttpClient outsider = await EventsTestIdentity.SignedIn(host.Factory, EventsTestIdentity.Outsider);
        using HttpClient owner = await EventsTestIdentity.SignedIn(host.Factory, EventsTestIdentity.Owner);
        using HttpClient anonymous = EventsTestIdentity.Anonymous(host.Factory);

        (await editor.GetAsync(Path)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await viewer.PostAsync($"{Path}/{Guid.NewGuid()}/send", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await outsider.GetAsync(Path)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await anonymous.GetAsync(Path)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await owner.GetAsync(Path.Replace("ocharo-fashion", "another-catalog", StringComparison.Ordinal)))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        digests.Catalogs.Should().BeEmpty();
    }

    [Fact]
    public async Task Delivery_ShouldAnswerUnavailable_InALocalRunWithoutEvents()
    {
        await using var host = EventsHost.Create(
            new Dictionary<string, string?> { ["Events:BaseUrl"] = null }, digests: new FakeDigestsBroker(), environment: "Development");
        using HttpClient owner = await EventsTestIdentity.SignedIn(host.Factory, EventsTestIdentity.Owner);

        (await owner.GetAsync(Path)).StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        string body = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue(body);
        using JsonDocument document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("data").Clone();
    }
}
