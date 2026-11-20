using System.Net;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Files.Store;
using WoW.Two.Sdk.Beta.Be.Files.Urls;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;

namespace OcharoEvents.Tests.E2E;

/// <summary>
/// The event image route: SCFD's stored copy of an event's image, served by Ocharo's own backend from WoW2 Files by a
/// read-only tenant key. The route reads the event through Events, takes the file id from <c>facts.images[index].fileRef</c>
/// and answers 302 to a short-lived signed Files URL; it authorizes as the event read does and answers 404 for a miss. With
/// Files unconfigured or failing, it answers 302 to the image's own https source link (404 for any other source), so a card
/// keeps its source photo until the copy is served.
/// </summary>
public sealed class EventImageRouteTests
{
    private static readonly string Catalog =
        $"/api/organizations/{EventsTestIdentity.Organization}/events-api/api/events/v1/catalogs/ocharo-fashion";
    private static readonly string OtherOrganizationCatalog =
        $"/api/organizations/{EventsTestIdentity.OtherOrganization}/events-api/api/events/v1/catalogs/ocharo-fashion";
    private static readonly Guid FashionWeek = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid Copy = Guid.Parse("0197a8a0-1111-7aaa-8bbb-000000000001");
    private static readonly Guid OtherCopy = Guid.Parse("0197a8a0-2222-7aaa-8bbb-000000000002");

    /// <summary>The settings that turn the copies on: a Files root and a read-only key (a test value, never a real one).</summary>
    private static readonly Dictionary<string, string?> FilesOn = new()
    {
        ["Files:BaseUrl"] = "https://files.example.test/",
        ["Files:ApiKey"] = "test-read-only-key",
    };

    private const string Source = "https://source.example.test/runway.jpg";

    /// <summary>An event whose first image is only the source link and whose second SCFD copied.</summary>
    private static RecordingEventsBrokers EventWithImages() => new()
    {
        Images =
        [
            new EventImage { Url = "https://source.example.test/poster.jpg" },
            new EventImage { Url = Source, FileRef = Copy.ToString("D") },
        ],
    };

    [Fact]
    public async Task Image_ShouldRedirectToTheSignedFilesUrl_OfTheCopyTheEventNamed()
    {
        var events = EventWithImages();
        var files = new FakeFileUrlsBroker();
        await using var host = EventsHost.Create(FilesOn, events, files: files);
        using var viewer = await EventsTestIdentity.SignedIn(host.Factory, EventsTestIdentity.Viewer);

        using var response = await viewer.GetAsync($"{Catalog}/events/{FashionWeek}/images/1");

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location.Should().Be(FakeFileUrlsBroker.LinkFor(new FileId(Copy)));
        response.Headers.CacheControl!.Private.Should().BeTrue();
        response.Headers.CacheControl.NoStore.Should().BeFalse();
        response.Headers.CacheControl.MaxAge.Should().Be(TimeSpan.FromSeconds(60));
        var call = events.Of("GetEventAsync").Should().ContainSingle().Which;
        call.Catalog.Should().Be("ocharo-fashion");
        call.Argument.Should().Be(FashionWeek);
        var issued = files.Issued.Should().ContainSingle().Which;
        issued.Id.Should().Be(new FileId(Copy));
        issued.Spec.Lifetime.Should().Be(TimeSpan.FromMinutes(5));
        issued.Spec.Disposition.Should().Be(FileDisposition.Inline);
    }

    [Fact]
    public async Task Image_ShouldTakeTheFileIdFromTheEvent_NeverFromTheBrowser()
    {
        var events = EventWithImages();
        var files = new FakeFileUrlsBroker();
        await using var host = EventsHost.Create(FilesOn, events, files: files);
        using var viewer = await EventsTestIdentity.SignedIn(host.Factory, EventsTestIdentity.Viewer);

        // Image 0 has no copy: a file id the browser adds names nothing, and image 1 still resolves to the event's own copy.
        using var named = await viewer.GetAsync($"{Catalog}/events/{FashionWeek}/images/0?fileRef={OtherCopy}&id={OtherCopy}");
        using var own = await viewer.GetAsync($"{Catalog}/events/{FashionWeek}/images/1?fileRef={OtherCopy}");

        named.StatusCode.Should().Be(HttpStatusCode.NotFound);
        own.StatusCode.Should().Be(HttpStatusCode.Found);
        files.Issued.Select(request => request.Id).Should().Equal(new FileId(Copy));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(99)]
    public async Task Image_ShouldAnswer404WithoutAskingFiles_WhenTheIndexIsOutOfRangeOrTheImageHasNoCopy(int index)
    {
        var files = new FakeFileUrlsBroker();
        await using var host = EventsHost.Create(FilesOn, EventWithImages(), files: files);
        using var viewer = await EventsTestIdentity.SignedIn(host.Factory, EventsTestIdentity.Viewer);

        using var response = await viewer.GetAsync($"{Catalog}/events/{FashionWeek}/images/{index}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        files.Issued.Should().BeEmpty();
    }

    [Fact]
    public async Task Image_ShouldAnswerTheSourceLink_WhenAnImagesFileRefIsNotAFileId()
    {
        var events = new RecordingEventsBrokers { Images = [new EventImage { Url = Source, FileRef = "not-a-guid" }] };
        var files = new FakeFileUrlsBroker();
        await using var host = EventsHost.Create(FilesOn, events, files: files);
        using var viewer = await EventsTestIdentity.SignedIn(host.Factory, EventsTestIdentity.Viewer);

        using var response = await viewer.GetAsync($"{Catalog}/events/{FashionWeek}/images/0");

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location.Should().Be(new Uri(Source));
        files.Issued.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("https://files.example.test/", null)]
    [InlineData(null, "test-read-only-key")]
    public async Task Image_ShouldRedirectToTheSourceLink_WhenFilesIsNotConfigured(string? baseUrl, string? apiKey)
    {
        var events = EventWithImages();
        var files = new FakeFileUrlsBroker();
        await using var host = EventsHost.Create(
            new Dictionary<string, string?> { ["Files:BaseUrl"] = baseUrl, ["Files:ApiKey"] = apiKey }, events, files: files);
        using var viewer = await EventsTestIdentity.SignedIn(host.Factory, EventsTestIdentity.Viewer);

        using var response = await viewer.GetAsync($"{Catalog}/events/{FashionWeek}/images/1");

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location.Should().Be(new Uri(Source));
        response.Headers.CacheControl!.Private.Should().BeTrue();
        response.Headers.CacheControl.MaxAge.Should().Be(TimeSpan.FromSeconds(60));
        events.Of("GetEventAsync").Should().ContainSingle();
        files.Issued.Should().BeEmpty();
    }

    [Fact]
    public async Task Image_ShouldStillAnswer404_ForAnImageWithNoFileRef_WhenFilesIsNotConfigured()
    {
        await using var host = EventsHost.Create(new Dictionary<string, string?>(), EventWithImages());
        using var viewer = await EventsTestIdentity.SignedIn(host.Factory, EventsTestIdentity.Viewer);

        using var withoutCopy = await viewer.GetAsync($"{Catalog}/events/{FashionWeek}/images/0");
        using var outOfRange = await viewer.GetAsync($"{Catalog}/events/{FashionWeek}/images/9");

        withoutCopy.StatusCode.Should().Be(HttpStatusCode.NotFound);
        outOfRange.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("http://source.example.test/runway.jpg")]
    [InlineData("ftp://source.example.test/runway.jpg")]
    [InlineData("/runway.jpg")]
    [InlineData("https://user:secret@source.example.test/runway.jpg")]
    public async Task Image_ShouldAnswer404_WhenFilesIsOutAndTheSourceIsNotAnAbsoluteHttpsLink(string source)
    {
        var events = new RecordingEventsBrokers { Images = [new EventImage { Url = source, FileRef = Copy.ToString("D") }] };
        var failing = new FakeFileUrlsBroker { Failure = AppErrorFactory.ExternalUnavailable("down") };
        await using var unconfigured = EventsHost.Create(new Dictionary<string, string?>(), events);
        await using var broken = EventsHost.Create(FilesOn, events, files: failing);
        using var first = await EventsTestIdentity.SignedIn(unconfigured.Factory, EventsTestIdentity.Viewer);
        using var second = await EventsTestIdentity.SignedIn(broken.Factory, EventsTestIdentity.Viewer);

        using var withoutFiles = await first.GetAsync($"{Catalog}/events/{FashionWeek}/images/0");
        using var withFailingFiles = await second.GetAsync($"{Catalog}/events/{FashionWeek}/images/0");

        withoutFiles.StatusCode.Should().Be(HttpStatusCode.NotFound);
        withFailingFiles.StatusCode.Should().Be(HttpStatusCode.NotFound);
        withFailingFiles.Headers.Location.Should().BeNull();
    }

    [Fact]
    public async Task Image_ShouldAnswer401_WhenNoSessionExists()
    {
        var events = EventWithImages();
        var files = new FakeFileUrlsBroker();
        await using var host = EventsHost.Create(FilesOn, events, files: files);
        using var anonymous = EventsTestIdentity.Anonymous(host.Factory);

        using var response = await anonymous.GetAsync($"{Catalog}/events/{FashionWeek}/images/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        events.Calls.Should().BeEmpty();
        files.Issued.Should().BeEmpty();
    }

    [Fact]
    public async Task Image_ShouldAnswer404_ForAnotherOrganizationAMemberWithoutEventsReadAndAnotherCatalog()
    {
        var events = EventWithImages();
        var files = new FakeFileUrlsBroker();
        await using var host = EventsHost.Create(FilesOn, events, files: files);

        // An Ocharo owner on another organization's path, another organization's owner and an Ocharo member without events:read.
        using var owner = await EventsTestIdentity.SignedIn(host.Factory, EventsTestIdentity.Owner);
        using (var response = await owner.GetAsync($"{OtherOrganizationCatalog}/events/{FashionWeek}/images/1"))
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        foreach (var person in new[] { EventsTestIdentity.Outsider, EventsTestIdentity.Unaffiliated, EventsTestIdentity.Billing })
        {
            using var client = await EventsTestIdentity.SignedIn(host.Factory, person);
            using var response = await client.GetAsync($"{Catalog}/events/{FashionWeek}/images/1");
            response.StatusCode.Should().Be(HttpStatusCode.NotFound, person.Subject);
        }
        // A catalog other than the page's is refused before Events.
        using (var response = await owner.GetAsync(
            $"/api/organizations/{EventsTestIdentity.Organization}/events-api/api/events/v1/catalogs/another/events/{FashionWeek}/images/1"))
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        events.Calls.Should().BeEmpty();
        files.Issued.Should().BeEmpty();
    }

    [Fact]
    public async Task Image_ShouldPassOnEventsRefusal_WithoutAskingFiles()
    {
        var events = EventWithImages();
        events.Failure = AppErrorFactory.NotFound("The event was not found.");
        var files = new FakeFileUrlsBroker();
        await using var host = EventsHost.Create(FilesOn, events, files: files);
        using var viewer = await EventsTestIdentity.SignedIn(host.Factory, EventsTestIdentity.Viewer);

        using var response = await viewer.GetAsync($"{Catalog}/events/{FashionWeek}/images/1");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        files.Issued.Should().BeEmpty();
    }

    [Fact]
    public async Task Image_ShouldRedirectToTheSourceLinkWithNoFilesDetail_WhenFilesFails()
    {
        var files = new FakeFileUrlsBroker { Failure = AppErrorFactory.ExternalUnavailable("files-internal-detail at 10.0.0.7") };
        await using var host = EventsHost.Create(FilesOn, EventWithImages(), files: files);
        using var viewer = await EventsTestIdentity.SignedIn(host.Factory, EventsTestIdentity.Viewer);

        using var response = await viewer.GetAsync($"{Catalog}/events/{FashionWeek}/images/1");

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location.Should().Be(new Uri(Source));
        (await response.Content.ReadAsStringAsync()).Should().NotContain("files-internal-detail").And.NotContain("test-read-only-key");
        files.Issued.Should().ContainSingle();
    }

    [Fact]
    public async Task Image_ShouldRedirectToTheSourceLink_WhenFilesIsUnreachableOrTimesOut()
    {
        var files = new FakeFileUrlsBroker { Throws = new TaskCanceledException("the request timed out") };
        await using var host = EventsHost.Create(FilesOn, EventWithImages(), files: files);
        using var viewer = await EventsTestIdentity.SignedIn(host.Factory, EventsTestIdentity.Viewer);

        using var response = await viewer.GetAsync($"{Catalog}/events/{FashionWeek}/images/1");

        response.StatusCode.Should().Be(HttpStatusCode.Found);
        response.Headers.Location.Should().Be(new Uri(Source));
    }
}
