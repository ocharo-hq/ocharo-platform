using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OcharoEvents.Infrastructure.Events.Services;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Files.Store;
using WoW.Two.Sdk.Beta.Be.Files.Urls;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace OcharoEvents.Tests.Unit;

/// <summary>How the event image route finds where a browser gets an image: the Files request is built from the event's own
/// <c>facts.images[index].fileRef</c>; an index out of range and an image with no <c>fileRef</c> answer not found; and with Files
/// unconfigured, failing, throwing or slow, the route answers the image's own https source link, or not found when that is not
/// an absolute https link, so a card keeps its source photo until the copy is served.</summary>
public sealed class EventImagesServiceTests
{
    private static readonly Guid EventId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid Copy = Guid.Parse("0197a8a0-1111-7aaa-8bbb-000000000001");
    private const string Source = "https://source.example.test/b.jpg";

    [Fact]
    public async Task GetImageLinkAsync_ShouldAskFilesForTheFileTheEventNamed_ForFiveMinutesInline()
    {
        var harness = new Harness([new EventImage { Url = "https://source.example.test/a.jpg" }, WithCopy(Copy)]);

        var link = await harness.Service.GetImageLinkAsync("ocharo-fashion", EventId, 1, CancellationToken.None);

        LinkOf(link).Should().Be(Harness.LinkFor(Copy));
        harness.Catalog.Reads.Should().Equal(("ocharo-fashion", EventId));
        var issued = harness.Files.Issued.Should().ContainSingle().Which;
        issued.Id.Should().Be(new FileId(Copy));
        issued.Spec.Lifetime.Should().Be(TimeSpan.FromMinutes(5));
        issued.Spec.Disposition.Should().Be(FileDisposition.Inline);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(2)]
    public async Task GetImageLinkAsync_ShouldAnswerNotFound_WhenTheIndexIsOutOfRangeOrTheImageHasNoFileRef(int index)
    {
        var harness = new Harness([new EventImage { Url = "https://source.example.test/a.jpg" }, WithCopy(Copy)]);

        var link = await harness.Service.GetImageLinkAsync("ocharo-fashion", EventId, index, CancellationToken.None);

        ErrorOf(link).Type.Should().Be(AppErrorType.NotFound);
        harness.Files.Issued.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GetImageLinkAsync_ShouldAnswerNotFound_WhenTheFileRefIsBlank_EvenWithASourceLink(string fileRef)
    {
        var harness = new Harness([new EventImage { Url = Source, FileRef = fileRef }]);

        var link = await harness.Service.GetImageLinkAsync("ocharo-fashion", EventId, 0, CancellationToken.None);

        ErrorOf(link).Type.Should().Be(AppErrorType.NotFound);
        harness.Files.Issued.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("https://files.example.test/", null)]
    [InlineData(null, "test-read-only-key")]
    [InlineData("not a url", "test-read-only-key")]
    public async Task GetImageLinkAsync_ShouldAnswerTheSourceLink_WhenFilesIsNotConfigured(string? baseUrl, string? apiKey)
    {
        var harness = new Harness([WithCopy(Copy)], new FilesServiceOptions { BaseUrl = baseUrl, ApiKey = apiKey });

        var link = await harness.Service.GetImageLinkAsync("ocharo-fashion", EventId, 0, CancellationToken.None);

        LinkOf(link).Should().Be(new Uri(Source));
        harness.Catalog.Reads.Should().Equal(("ocharo-fashion", EventId));
        harness.Files.Issued.Should().BeEmpty();
    }

    [Fact]
    public async Task GetImageLinkAsync_ShouldAnswerTheSourceLink_WhenTheFileRefIsNoFileId()
    {
        var harness = new Harness([new EventImage { Url = Source, FileRef = "not-a-guid" }]);

        var link = await harness.Service.GetImageLinkAsync("ocharo-fashion", EventId, 0, CancellationToken.None);

        LinkOf(link).Should().Be(new Uri(Source));
        harness.Files.Issued.Should().BeEmpty();
    }

    [Fact]
    public async Task GetImageLinkAsync_ShouldAnswerTheSourceLink_WhenFilesRefuses()
    {
        var harness = new Harness([WithCopy(Copy)]);
        harness.Files.Failure = AppErrorFactory.ExternalUnavailable("internal detail from files");

        var link = await harness.Service.GetImageLinkAsync("ocharo-fashion", EventId, 0, CancellationToken.None);

        LinkOf(link).Should().Be(new Uri(Source));
        harness.Files.Issued.Should().ContainSingle();
    }

    [Fact]
    public async Task GetImageLinkAsync_ShouldAnswerTheSourceLink_WhenFilesThrows()
    {
        var harness = new Harness([WithCopy(Copy)]);
        harness.Files.Throws = new HttpRequestException("connection refused");

        var link = await harness.Service.GetImageLinkAsync("ocharo-fashion", EventId, 0, CancellationToken.None);

        LinkOf(link).Should().Be(new Uri(Source));
    }

    [Fact]
    public async Task GetImageLinkAsync_ShouldAnswerTheSourceLink_WhenFilesAnswersTooLate()
    {
        var harness = new Harness([WithCopy(Copy)], new FilesServiceOptions
        {
            BaseUrl = "https://files.example.test/",
            ApiKey = "test-read-only-key",
            LinkTimeout = TimeSpan.FromMilliseconds(50),
        });
        harness.Files.Hangs = true;

        var link = await harness.Service.GetImageLinkAsync("ocharo-fashion", EventId, 0, CancellationToken.None);

        LinkOf(link).Should().Be(new Uri(Source));
    }

    [Fact]
    public async Task GetImageLinkAsync_ShouldStayCancelled_WhenTheCallerCancels()
    {
        var harness = new Harness([WithCopy(Copy)]);
        harness.Files.Hangs = true;
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        var call = () => harness.Service.GetImageLinkAsync("ocharo-fashion", EventId, 0, cancellation.Token);

        await call.Should().ThrowAsync<OperationCanceledException>();
    }

    [Theory]
    [InlineData("http://source.example.test/b.jpg")]
    [InlineData("ftp://source.example.test/b.jpg")]
    [InlineData("/relative/b.jpg")]
    [InlineData("https://user:secret@source.example.test/b.jpg")]
    [InlineData("not a url")]
    [InlineData("")]
    public async Task GetImageLinkAsync_ShouldAnswerNotFound_WhenFilesIsOutAndTheSourceIsNotAnAbsoluteHttpsLink(string url)
    {
        var unconfigured = new Harness([new EventImage { Url = url, FileRef = Copy.ToString("D") }], new FilesServiceOptions());
        var failing = new Harness([new EventImage { Url = url, FileRef = Copy.ToString("D") }]);
        failing.Files.Failure = AppErrorFactory.ExternalUnavailable("down");

        ErrorOf(await unconfigured.Service.GetImageLinkAsync("ocharo-fashion", EventId, 0, CancellationToken.None))
            .Type.Should().Be(AppErrorType.NotFound);
        ErrorOf(await failing.Service.GetImageLinkAsync("ocharo-fashion", EventId, 0, CancellationToken.None))
            .Type.Should().Be(AppErrorType.NotFound);
    }

    [Fact]
    public async Task GetImageLinkAsync_ShouldNotUseTheSourceLink_WhenFilesServesTheCopy()
    {
        var harness = new Harness([new EventImage { Url = "http://source.example.test/b.jpg", FileRef = Copy.ToString("D") }]);

        var link = await harness.Service.GetImageLinkAsync("ocharo-fashion", EventId, 0, CancellationToken.None);

        LinkOf(link).Should().Be(Harness.LinkFor(Copy));
    }

    [Fact]
    public async Task GetImageLinkAsync_ShouldRefuseAnotherCatalog_BeforeReachingEvents()
    {
        var harness = new Harness([WithCopy(Copy)]);

        var link = await harness.Service.GetImageLinkAsync("another", EventId, 0, CancellationToken.None);

        ErrorOf(link).Type.Should().Be(AppErrorType.NotFound);
        harness.Catalog.Reads.Should().BeEmpty();
        harness.Files.Issued.Should().BeEmpty();
    }

    private static EventImage WithCopy(Guid file) => new() { Url = Source, FileRef = file.ToString("D") };

    private static Uri LinkOf(Result<Uri> result) => result.Should().BeOfType<Result<Uri>.Success>().Which.Value;

    private static AppError ErrorOf(Result<Uri> result) => result.Should().BeOfType<Result<Uri>.Failure>().Which.Error;

    /// <summary>The service over a fake Events read of one event and a fake Files; the brokers the page's mirror does not
    /// use here are never called.</summary>
    private sealed class Harness
    {
        public Harness(IReadOnlyList<EventImage> images, FilesServiceOptions? files = null)
        {
            Catalog = new FakeCatalogEvents(images);
            var events = new EventsApiService(
                tracking: null!, Catalog, workflows: null!, calendar: null!, intake: null!,
                Options.Create(new EventsServiceOptions { BaseUrl = "https://events.example.test/", Catalog = "ocharo-fashion" }));
            var provider = new ServiceCollection().AddSingleton<IFileUrlsBroker>(Files).BuildServiceProvider();
            Service = new EventImagesService(
                events, provider,
                Options.Create(files ?? new FilesServiceOptions { BaseUrl = "https://files.example.test/", ApiKey = "test-read-only-key" }),
                NullLogger<EventImagesService>.Instance);
        }

        public FakeCatalogEvents Catalog { get; }

        public FakeFileUrls Files { get; } = new();

        public EventImagesService Service { get; }

        public static Uri LinkFor(Guid file) => new($"https://files.example.test/f/{file}?sig=test");
    }

    private sealed class FakeFileUrls : IFileUrlsBroker
    {
        public List<(FileId Id, UrlSpec Spec)> Issued { get; } = [];

        public AppError? Failure { get; set; }

        public Exception? Throws { get; set; }

        public bool Hangs { get; set; }

        public async Task<Result<SignedFileUrl>> IssueAsync(FileId id, UrlSpec spec, CancellationToken cancellationToken = default)
        {
            Issued.Add((id, spec));
            if (Throws is { } exception) throw exception;
            if (Hangs) await Task.Delay(Timeout.Infinite, cancellationToken);
            return Failure is { } failure
                ? Result<SignedFileUrl>.Fail(failure)
                : Result<SignedFileUrl>.Ok(new SignedFileUrl(Harness.LinkFor(id.Value), DateTimeOffset.UtcNow.Add(spec.Lifetime)));
        }

        public Task<Result<Uri>> GetPublicUrlAsync(FileId id, int? width = null, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Not used.");
    }

    private sealed class FakeCatalogEvents(IReadOnlyList<EventImage> images) : ICatalogEventsBroker
    {
        public List<(string Catalog, Guid Id)> Reads { get; } = [];

        public Task<Result<EventModel>> GetAsync(string catalog, Guid id, CancellationToken cancellationToken = default)
        {
            Reads.Add((catalog, id));
            var at = new DateTimeOffset(2026, 11, 20, 9, 0, 0, TimeSpan.Zero);
            return Task.FromResult(Result<EventModel>.Ok(new EventModel
            {
                Id = id,
                Catalog = catalog,
                Facts = new EventFacts { Title = new Dictionary<string, string> { ["und"] = "Tashkent Fashion Week" }, Images = images },
                Review = EventReviewState.Published,
                Revision = 1,
                CreatedAtUtc = at,
                UpdatedAtUtc = at,
            }));
        }

        public Task<Result<EventPage>> ListAsync(string catalog, EventQuery query, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Not used.");

        public Task<Result<EventModel>> CreateAsync(string catalog, EventSpec spec, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Not used.");

        public Task<Result<EventModel>> ProposeAsync(string catalog, EventSpec spec, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Not used.");

        public Task<Result<EventModel>> UpdateAsync(string catalog, Guid id, EventSpec spec, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Not used.");

        public Task<Result<EventModel>> ArchiveAsync(string catalog, Guid id, int expectedRevision, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Not used.");
    }
}
