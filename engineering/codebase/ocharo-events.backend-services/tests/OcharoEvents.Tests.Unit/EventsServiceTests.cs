using System.Text.Json;
using OcharoEvents.Application.Access.Models;
using OcharoEvents.Application.Events.Models;
using OcharoEvents.Domain.Events.Models;
using OcharoEvents.Infrastructure.Events.Services;
using Microsoft.Extensions.Options;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Tracking;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Results;

namespace OcharoEvents.Tests.Unit;

/// <summary>
/// Checks the events port served from WoW2 Events (events-adoption.md #7, #8), moved from Desk: reads map Events'
/// catalog and tracking onto the page's record, tracking writes pass, fact edits and deletes answer Gone.
/// </summary>
public sealed class EventsServiceTests
{
    private static readonly Guid Organization = Guid.NewGuid();
    private static readonly CancellationToken Token = CancellationToken.None;
    private static readonly ShowcaseEventFilter All = new(null, null, null, null, null, null, true);

    [Fact]
    public async Task Read_ShouldShowAnAcceptedUntrackedEventAsACandidate_AndHideRejectedOnes()
    {
        var service = new FakeEventsService();
        EventModel accepted = service.Add(Facts("Afisha: Tashkent Fashion Week", "UZ", Day("2026-12-03")));
        service.Add(Facts("Rejected fair", "UZ", Day("2026-12-04")), EventReviewState.Rejected);

        IReadOnlyList<ShowcaseEvent> shown = await Repository(service).GetEventsAsync(Organization, All, 200, 0, Token);

        ShowcaseEvent item = shown.Should().ContainSingle().Which;
        item.Id.Should().Be(accepted.Id);
        item.Status.Should().Be("candidate");
        item.Priority.Should().Be("normal");
        item.Revision.Should().Be(1);
        item.Scope.Should().Be("local");
        item.StartsOn.Should().Be(new DateOnly(2026, 12, 3));
        item.DatePrecision.Should().Be("day");
    }

    [Theory]
    [InlineData("UZ", "local")]
    [InlineData("KZ", "regional")]
    [InlineData("RU", "regional")]
    [InlineData("TR", "international")]
    [InlineData(null, "international")]
    public void Scope_ShouldFollowTheCountry(string? country, string scope) =>
        EventsServiceMapper.Scope(country).Should().Be(scope);

    [Fact]
    public void Map_ShouldKeepMonthAndEstimatePrecision_TrackingAndFields()
    {
        var item = new EventModel
        {
            Id = Guid.NewGuid(), Catalog = "ocharo-fashion", Review = EventReviewState.Published, Revision = 2,
            CreatedAtUtc = DateTimeOffset.UnixEpoch, UpdatedAtUtc = DateTimeOffset.UnixEpoch,
            Facts = Facts("Visa Fashion Week Almaty", "KZ", new EventWhen
            {
                Value = "2027-04", Precision = DatePrecision.Month, Certainty = DateCertainty.Estimated,
            }) with
            {
                Title = new Dictionary<string, string> { ["ru"] = "Неделя моды", ["en"] = "Visa Fashion Week Almaty" },
                Deadlines = [new EventDeadline { Kind = DeadlineKind.Application, At = Day("2027-02-15") }],
            },
        };
        var tracking = new TrackingModel
        {
            Id = Guid.NewGuid(), EventId = item.Id, Stage = "applying", Priority = TrackingPriority.High,
            Notes = "Fit: ikat.", Revision = 3, CreatedAtUtc = DateTimeOffset.UnixEpoch, UpdatedAtUtc = DateTimeOffset.UnixEpoch,
            Fields = JsonSerializer.SerializeToElement(new { participation = "Stand booking", cost = "USD 500" }),
        };

        ShowcaseEvent shown = EventsServiceMapper.ToShowcaseEvent(new TrackedEventModel { Event = item, Tracking = tracking });

        shown.Title.Should().Be("Visa Fashion Week Almaty");
        shown.StartsOn.Should().Be(new DateOnly(2027, 4, 1));
        shown.DatePrecision.Should().Be("estimated");
        shown.ApplicationDeadline.Should().Be(new DateOnly(2027, 2, 15));
        shown.Scope.Should().Be("regional");
        shown.Status.Should().Be("applying");
        shown.Priority.Should().Be("high");
        shown.Notes.Should().Be("Fit: ikat.");
        shown.Participation.Should().Be("Stand booking");
        shown.Cost.Should().Be("USD 500");
        shown.Revision.Should().Be(4);
    }

    [Theory]
    [InlineData("day", "2027-01-29")]
    [InlineData("month", "2027-05-01")]
    [InlineData("estimated", "2027-04-01")]
    [InlineData("unknown", null)]
    public void Facts_ShouldRoundTripThroughEvents_SoAStatusChangeKeepsThem(string precision, string? startsOn)
    {
        ShowcaseEventInput input = Input(precision, startsOn is null ? null : DateOnly.Parse(startsOn));
        var item = new EventModel
        {
            Id = Guid.NewGuid(), Catalog = "ocharo-fashion", Facts = EventsServiceMapper.ToFacts(input),
            Review = EventReviewState.Candidate, Revision = 1,
            CreatedAtUtc = DateTimeOffset.UnixEpoch, UpdatedAtUtc = DateTimeOffset.UnixEpoch,
        };

        ShowcaseEvent shown = EventsServiceMapper.ToShowcaseEvent(new TrackedEventModel { Event = item });

        EventsServiceMapper.HasSameFacts(shown, input with { SourceUrl = null }).Should().BeTrue();
    }

    [Fact]
    public async Task Update_ShouldWriteTracking_WhenOnlyStagePriorityOrNotesChange()
    {
        var service = new FakeEventsService();
        EventModel item = service.Add(Facts("Ambiente Frankfurt", "DE", Day("2027-01-29")));
        var repository = Repository(service);
        ShowcaseEvent shown = (await repository.GetEventsAsync(Organization, All, 200, 0, Token)).Single();

        Result<ShowcaseEvent> first = await repository.UpdateEventAsync(
            Organization, item.Id, shown.Revision, InputOf(shown) with { Status = "shortlisted", Notes = "Ask for a stand." }, Token);
        ShowcaseEvent saved = first.Should().BeOfType<Result<ShowcaseEvent>.Success>().Which.Value;
        Result<ShowcaseEvent> second = await repository.UpdateEventAsync(
            Organization, item.Id, saved.Revision, InputOf(saved) with { Priority = "high" }, Token);

        service.TrackingWrites.Select(write => write.ExpectedRevision).Should().Equal(null, 1);
        saved.Status.Should().Be("shortlisted");
        saved.Revision.Should().Be(2);
        second.Should().BeOfType<Result<ShowcaseEvent>.Success>().Which.Value.Revision.Should().Be(3);
    }

    [Fact]
    public async Task Update_ShouldAnswerGone_WhenAFactChanges_AndConflict_WhenStale()
    {
        var service = new FakeEventsService();
        EventModel item = service.Add(Facts("Ambiente Frankfurt", "DE", Day("2027-01-29")));
        var repository = Repository(service);
        ShowcaseEvent shown = (await repository.GetEventsAsync(Organization, All, 200, 0, Token)).Single();

        Result<ShowcaseEvent> moved = await repository.UpdateEventAsync(
            Organization, item.Id, shown.Revision, InputOf(shown) with { Title = "Ambiente 2027" }, Token);
        Result<ShowcaseEvent> stale = await repository.UpdateEventAsync(
            Organization, item.Id, shown.Revision + 1, InputOf(shown), Token);
        Result deleted = await repository.DeleteEventAsync(Organization, item.Id, shown.Revision, Token);

        moved.Should().BeOfType<Result<ShowcaseEvent>.Failure>().Which.Error.Type.Should().Be(AppErrorType.Gone);
        stale.Should().BeOfType<Result<ShowcaseEvent>.Failure>().Which.Error.Type.Should().Be(AppErrorType.Conflict);
        deleted.Should().BeOfType<Result.Failure>().Which.Error.Type.Should().Be(AppErrorType.Gone);
        service.TrackingWrites.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_ShouldProposeACandidate_AndTrackIt()
    {
        var service = new FakeEventsService();

        Result<ShowcaseEvent> created = await Repository(service).CreateEventAsync(
            Organization, "user-1", Input("day", new DateOnly(2027, 3, 10)) with { Status = "shortlisted" }, Token);

        ShowcaseEvent item = created.Should().BeOfType<Result<ShowcaseEvent>.Success>().Which.Value;
        service.Event(item.Id).Review.Should().Be(EventReviewState.Candidate);
        service.Event(item.Id).Facts.Title.Should().Equal(new Dictionary<string, string> { ["und"] = "Ikat Week" });
        item.Status.Should().Be("shortlisted");
        item.Revision.Should().Be(2);
        item.SourceUrl.Should().Be("https://example.org/found");
    }

    [Fact]
    public async Task Read_ShouldFollowTheCursor_ApplyThePageFiltersAndSummarize()
    {
        var service = new FakeEventsService { PageSize = 2 };
        for (var day = 1; day <= 5; day++)
            service.Add(Facts($"Fair {day}", day % 2 == 0 ? "UZ" : "FR", Day($"2027-06-{day:00}")) with
            {
                Deadlines = [new EventDeadline { Kind = DeadlineKind.Application, At = Day($"2027-05-{day:00}") }],
            });
        EventModel skipped = service.Add(Facts("Skipped fair", "FR", Day("2027-06-20")) with
        {
            Deadlines = [new EventDeadline { Kind = DeadlineKind.Application, At = Day("2027-04-01") }],
        });
        service.Track(skipped.Id, "skipped");
        var repository = Repository(service);

        IReadOnlyList<ShowcaseEvent> local = await repository.GetEventsAsync(
            Organization, All with { Scope = "local" }, 200, 0, Token);
        IReadOnlyList<ShowcaseEvent> second = await repository.GetEventsAsync(Organization, All, 2, 1, Token);
        ShowcaseEventSummary summary = await repository.GetSummaryAsync(Organization, new DateOnly(2027, 1, 1), 3, Token);

        local.Select(item => item.Title).Should().Equal("Fair 2", "Fair 4");
        second.Select(item => item.Title).Should().Equal("Fair 2", "Fair 3");
        summary.ByStatus.Should().Equal(new Dictionary<string, int> { ["candidate"] = 5, ["skipped"] = 1 });
        summary.UpcomingDeadlines.Select(item => item.Title).Should().Equal("Fair 1", "Fair 2", "Fair 3");
    }

    [Fact]
    public void Validation_ShouldTakeEventsNameLimits()
    {
        var fields = new EventFields(
            "Ikat Week", new string('o', 180), "local", "festival", "UZ", new string('c', 150), null, null, null,
            "unknown", null, "", null, null, null, "shortlisted", "normal", "");
        var validation = new EventValidationService();

        ShowcaseEventInput accepted = validation.Event(fields);

        accepted.Organizer.Should().HaveLength(180);
        accepted.City.Should().HaveLength(150);
        validation.Invoking(service => service.Event(fields with { Organizer = new string('o', 201) }))
            .Should().Throw<AppException>().Which.Error.Type.Should().Be(AppErrorType.Validation);
    }

    [Fact]
    public async Task Write_ShouldAnswerForbiddenBeforeEvents_WithoutTheTrackPermission()
    {
        var service = new FakeEventsService();
        EventModel item = service.Add(Facts("Ambiente Frankfurt", "DE", Day("2027-01-29")));
        var events = new EventService(Repository(service), new EventValidationService(), TimeProvider.System);
        var reader = new OrganizationAccess(Organization, "viewer-sub", CanTrack: false);

        Func<Task> propose = () => events.CreateEventAsync(reader, Input("unknown", null), Token);
        Func<Task> track = () => events.UpdateEventAsync(reader, item.Id, 1, Input("unknown", null), Token);

        (await propose.Should().ThrowAsync<AppException>()).Which.Error.Type.Should().Be(AppErrorType.Forbidden);
        (await track.Should().ThrowAsync<AppException>()).Which.Error.Type.Should().Be(AppErrorType.Forbidden);
        service.TrackingWrites.Should().BeEmpty();
        (await events.GetEventsAsync(reader, null, null, null, null, null, null, true, 200, 0, Token)).Should().ContainSingle();
    }

    [Fact]
    public async Task Read_ShouldAnswerUnavailable_WhenEventsIsNotConfigured()
    {
        var repository = new EventsServiceRepository(
            new FakeEventsService(), new FakeEventsService(), Options.Create(new EventsServiceOptions()));

        Func<Task> read = () => repository.GetEventsAsync(Organization, All, 200, 0, Token);

        (await read.Should().ThrowAsync<AppException>()).Which.Error.Type.Should().Be(AppErrorType.ExternalUnavailable);
    }

    private static EventsServiceRepository Repository(FakeEventsService service) =>
        new(service, service, Options.Create(new EventsServiceOptions { BaseUrl = "https://events.example.test" }));

    private static EventFacts Facts(string title, string? country, EventWhen starts) => new()
    {
        Title = new Dictionary<string, string> { ["und"] = title },
        Kind = "trade-fair",
        Place = country is null ? null : new EventPlace { Country = country },
        Starts = starts,
    };

    private static EventWhen Day(string value) => new() { Value = value, Precision = DatePrecision.Day };

    private static ShowcaseEventInput Input(string precision, DateOnly? startsOn) => new(
        "Ikat Week", "Ikat House", "local", "festival", "UZ", "Margilan", null, startsOn, null, precision,
        startsOn?.AddDays(-30), "Application package", "Free", "https://example.org", "https://example.org/found",
        "candidate", "normal", "Bring scarves.");

    private static ShowcaseEventInput InputOf(ShowcaseEvent item) => new(
        item.Title, item.Organizer, item.Scope, item.Format, item.Country, item.City, item.Venue, item.StartsOn,
        item.EndsOn, item.DatePrecision, item.ApplicationDeadline, item.Participation, item.Cost, item.Website,
        item.SourceUrl, item.Status, item.Priority, item.Notes);
}
