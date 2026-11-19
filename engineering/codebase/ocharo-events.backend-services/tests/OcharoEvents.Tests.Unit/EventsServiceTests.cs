using System.Text.Json;
using OcharoEvents.Application.Access.Models;
using OcharoEvents.Api.Events;
using OcharoEvents.Application.Events.Models;
using OcharoEvents.Application.Events.Services;
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
    [InlineData(null, "unknown")]
    [InlineData("", "unknown")]
    [InlineData(" ", "unknown")]
    public void Scope_ShouldFollowTheCountry(string? country, string scope) =>
        EventsServiceMapper.Scope(country).Should().Be(scope);

    [Fact]
    public async Task Read_ShouldListAnEventWithNoCountryAsUnknownScope_OutsideEveryScopeFilter()
    {
        var service = new FakeEventsService();
        service.Add(Facts("Online crafts market", null, Day("2026-12-05")));
        service.Add(Facts("Tashkent Fashion Week", "UZ", Day("2026-12-03")));
        var repository = Repository(service);

        IReadOnlyList<ShowcaseEvent> all = await repository.GetEventsAsync(Organization, All, 200, 0, Token);
        IReadOnlyList<ShowcaseEvent> international = await repository.GetEventsAsync(
            Organization, All with { Scope = "international" }, 200, 0, Token);

        all.Single(item => item.Title == "Online crafts market").Scope.Should().Be("unknown");
        international.Should().BeEmpty();
    }

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

    /// <summary>SCFD's estimated date (Events E34) reaches the page at any precision it was estimated at: an estimate never reads
    /// as an exact day or time, and an announced date keeps its day and time.</summary>
    [Theory]
    [InlineData(DatePrecision.Day, DateCertainty.Estimated, "2027-04-12", "2027-04-01", "estimated", null)]
    [InlineData(DatePrecision.Minute, DateCertainty.Estimated, "2027-04-12T19:00", "2027-04-01", "estimated", null)]
    [InlineData(DatePrecision.Month, DateCertainty.Estimated, "2027-04", "2027-04-01", "estimated", null)]
    [InlineData(DatePrecision.Day, DateCertainty.Announced, "2027-04-12", "2027-04-12", "day", null)]
    [InlineData(DatePrecision.Minute, DateCertainty.Announced, "2027-04-12T19:00", "2027-04-12", "day", "19:00")]
    [InlineData(DatePrecision.Month, DateCertainty.Announced, "2027-04", "2027-04-01", "month", null)]
    public void Map_ShouldNeverShowAnEstimatedStartAsExact(
        DatePrecision precision, DateCertainty certainty, string value, string startsOn, string shownPrecision, string? startsAt)
    {
        var item = new EventModel
        {
            Id = Guid.NewGuid(), Catalog = "ocharo-fashion", Review = EventReviewState.Published, Revision = 1,
            CreatedAtUtc = DateTimeOffset.UnixEpoch, UpdatedAtUtc = DateTimeOffset.UnixEpoch,
            Facts = Facts("Tashkent Fashion Week", "UZ", new EventWhen { Value = value, Precision = precision, Certainty = certainty }),
        };

        ShowcaseEvent shown = EventsServiceMapper.ToShowcaseEvent(new TrackedEventModel { Event = item });

        shown.StartsOn.Should().Be(DateOnly.Parse(startsOn));
        shown.DatePrecision.Should().Be(shownPrecision);
        shown.StartsAt.Should().Be(startsAt);
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
            Organization, item.Id, shown.Revision, null, InputOf(shown) with { Status = "shortlisted", Notes = "Ask for a stand." }, Token);
        ShowcaseEvent saved = first.Should().BeOfType<Result<ShowcaseEvent>.Success>().Which.Value;
        Result<ShowcaseEvent> second = await repository.UpdateEventAsync(
            Organization, item.Id, saved.Revision, null, InputOf(saved) with { Priority = "high" }, Token);

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
            Organization, item.Id, shown.Revision, shown.EventRevision, InputOf(shown) with { Title = "Ambiente 2027" }, Token);
        Result<ShowcaseEvent> legacyMoved = await repository.UpdateEventAsync(
            Organization, item.Id, shown.Revision, null, InputOf(shown) with { Title = "Ambiente 2027" }, Token);
        Result<ShowcaseEvent> stale = await repository.UpdateEventAsync(
            Organization, item.Id, shown.Revision + 1, null, InputOf(shown), Token);
        Result deleted = await repository.DeleteEventAsync(Organization, item.Id, shown.Revision, Token);

        moved.Should().BeOfType<Result<ShowcaseEvent>.Failure>().Which.Error.Type.Should().Be(AppErrorType.Gone);
        legacyMoved.Should().BeOfType<Result<ShowcaseEvent>.Failure>().Which.Error.Type.Should().Be(AppErrorType.Gone);
        stale.Should().BeOfType<Result<ShowcaseEvent>.Failure>().Which.Error.Type.Should().Be(AppErrorType.Conflict);
        deleted.Should().BeOfType<Result.Failure>().Which.Error.Type.Should().Be(AppErrorType.Gone);
        service.TrackingWrites.Should().BeEmpty();
    }

    [Fact]
    public async Task Read_ShouldPickTheTitleInTheUiLanguage_AndNameItsLanguage()
    {
        var service = new FakeEventsService();
        service.Add(Facts("ignored", "UZ", Day("2026-12-05")) with
        {
            Title = new Dictionary<string, string> { ["ru-Cyrl"] = "Неделя моды", ["uz-Latn"] = "Moda haftaligi" },
            Languages = ["ru-Cyrl"],
        });

        ShowcaseEvent uz = (await Repository(service, "uz").GetEventsAsync(Organization, All, 200, 0, Token)).Single();
        ShowcaseEvent ru = (await Repository(service, "ru-RU").GetEventsAsync(Organization, All, 200, 0, Token)).Single();
        ShowcaseEvent en = (await Repository(service, "en").GetEventsAsync(Organization, All, 200, 0, Token)).Single();
        ShowcaseEvent none = (await Repository(service).GetEventsAsync(Organization, All, 200, 0, Token)).Single();

        uz.Title.Should().Be("Moda haftaligi");
        uz.TitleLanguage.Should().Be("uz-Latn");
        uz.Languages.Should().Equal("ru-Cyrl");
        ru.Title.Should().Be("Неделя моды");
        ru.TitleLanguage.Should().Be("ru-Cyrl");
        en.TitleLanguage.Should().Be("uz-Latn");
        none.TitleLanguage.Should().Be("uz-Latn");
    }

    [Fact]
    public void Map_ShouldShowWhetherTheEventStillHappensAndWhereItIsAttended()
    {
        TrackedEventModel Tracked(EventFacts facts) => new()
        {
            Event = new EventModel
            {
                Id = Guid.NewGuid(), Catalog = "ocharo-fashion", Facts = facts, Review = EventReviewState.Published,
                Revision = 1, CreatedAtUtc = DateTimeOffset.UnixEpoch, UpdatedAtUtc = DateTimeOffset.UnixEpoch,
            },
        };
        EventFacts facts = Facts("Online craft fair", "UZ", Day("2026-12-05"));

        ShowcaseEvent cancelled = EventsServiceMapper.ToShowcaseEvent(Tracked(facts with
        {
            Status = EventStatus.Cancelled, Attendance = EventAttendance.Online,
        }));
        ShowcaseEvent plain = EventsServiceMapper.ToShowcaseEvent(Tracked(facts));

        cancelled.EventStatus.Should().Be("cancelled");
        cancelled.Attendance.Should().Be("online");
        plain.EventStatus.Should().Be("scheduled");
        plain.Attendance.Should().Be("offline");
        EventsServiceMapper.EventStatusName(EventStatus.Postponed).Should().Be("postponed");
        EventsServiceMapper.EventStatusName(EventStatus.Rescheduled).Should().Be("rescheduled");
        EventsServiceMapper.EventStatusName(EventStatus.MovedOnline).Should().Be("moved-online");
        EventsServiceMapper.AttendanceName(EventAttendance.Mixed).Should().Be("mixed");
    }

    [Fact]
    public void Map_ShouldShowTheSourcesTicketPriceApartFromOcharosOwnCost()
    {
        TrackedEventModel Tracked(EventFacts facts, TrackingModel? tracking = null) => new()
        {
            Event = new EventModel
            {
                Id = Guid.NewGuid(), Catalog = "ocharo-fashion", Facts = facts, Review = EventReviewState.Published,
                Revision = 1, CreatedAtUtc = DateTimeOffset.UnixEpoch, UpdatedAtUtc = DateTimeOffset.UnixEpoch,
            },
            Tracking = tracking,
        };
        EventFacts facts = Facts("Ikat Week", "UZ", Day("2026-12-05"));
        var booth = new EventOffer { Kind = OfferKind.Exhibitor, MinPrice = 900, MaxPrice = 900, Currency = "USD" };
        var ticket = new EventOffer
        {
            Kind = OfferKind.Ticket, MinPrice = 150000, MaxPrice = 150000, Currency = "UZS", Url = "https://tickets.example.test/ikat",
        };

        ShowcaseEvent priced = EventsServiceMapper.ToShowcaseEvent(Tracked(facts with
        {
            Offers = [booth, ticket], TicketUrl = "https://tickets.example.test/week",
        }));
        ShowcaseEvent offerLink = EventsServiceMapper.ToShowcaseEvent(Tracked(facts with { Offers = [booth, ticket] }));
        ShowcaseEvent free = EventsServiceMapper.ToShowcaseEvent(Tracked(facts with
        {
            Offers = [new EventOffer { Kind = OfferKind.Ticket, Free = true }],
        }));
        ShowcaseEvent onlyBooth = EventsServiceMapper.ToShowcaseEvent(Tracked(facts with { Offers = [booth] }));
        ShowcaseEvent none = EventsServiceMapper.ToShowcaseEvent(Tracked(facts, new TrackingModel
        {
            Id = Guid.NewGuid(), EventId = Guid.NewGuid(), Stage = "applying", Priority = TrackingPriority.Normal, Revision = 1,
            CreatedAtUtc = DateTimeOffset.UnixEpoch, UpdatedAtUtc = DateTimeOffset.UnixEpoch,
            Fields = JsonSerializer.SerializeToElement(new { cost = "Stand from 500 USD" }),
        }));

        priced.Tickets.Should().Be(new ShowcaseTickets(150000, 150000, "UZS", false));
        priced.TicketUrl.Should().Be("https://tickets.example.test/week");
        priced.Cost.Should().BeNull();
        offerLink.TicketUrl.Should().Be("https://tickets.example.test/ikat");
        free.Tickets.Should().Be(new ShowcaseTickets(null, null, null, true));
        onlyBooth.Tickets.Should().Be(new ShowcaseTickets(900, 900, "USD", false));
        onlyBooth.TicketUrl.Should().BeNull();
        none.Tickets.Should().BeNull();
        none.Cost.Should().Be("Stand from 500 USD");
    }

    [Fact]
    public void Map_ShouldShowTheStartTimeZoneAddressAndOrganizerSite_OfAMinuteStart()
    {
        TrackedEventModel Tracked(EventFacts facts) => new()
        {
            Event = new EventModel
            {
                Id = Guid.NewGuid(), Catalog = "ocharo-fashion", Facts = facts, Review = EventReviewState.Published,
                Revision = 1, CreatedAtUtc = DateTimeOffset.UnixEpoch, UpdatedAtUtc = DateTimeOffset.UnixEpoch,
            },
        };
        // The golden Afisha jazz evening: SCFD's `2026-12-05T19:00:00+05:00` arrives as a local minute in Asia/Tashkent (E08).
        EventFacts jazz = Facts("Jazz evening", "UZ", new EventWhen { Value = "2026-12-05T19:00", Precision = DatePrecision.Minute }) with
        {
            TimeZone = "Asia/Tashkent",
            Place = new EventPlace { Venue = "Ilkhom Theatre", Address = "Pakhtakor street 5", City = "Tashkent", Country = "UZ" },
            Organizer = new EventOrganizer { Name = "Jazz Club", Website = "https://jazz.example.test/" },
        };

        ShowcaseEvent timed = EventsServiceMapper.ToShowcaseEvent(Tracked(jazz));
        ShowcaseEvent dayOnly = EventsServiceMapper.ToShowcaseEvent(Tracked(jazz with
        {
            Starts = Day("2026-12-05"), TimeZone = null, Place = null, Organizer = new EventOrganizer { Name = "Jazz Club" },
        }));

        timed.StartsOn.Should().Be(new DateOnly(2026, 12, 5));
        timed.DatePrecision.Should().Be("day");
        timed.StartsAt.Should().Be("19:00");
        timed.TimeZone.Should().Be("Asia/Tashkent");
        timed.Address.Should().Be("Pakhtakor street 5");
        timed.Venue.Should().Be("Ilkhom Theatre");
        timed.OrganizerWebsite.Should().Be("https://jazz.example.test/");
        dayOnly.StartsAt.Should().BeNull();
        dayOnly.TimeZone.Should().BeNull();
        dayOnly.Address.Should().BeNull();
        dayOnly.OrganizerWebsite.Should().BeNull();
        EventsServiceMapper.StartTime(new EventWhen { Value = "2026-12", Precision = DatePrecision.Month }).Should().BeNull();
        EventsServiceMapper.StartTime(new EventWhen { Value = "2026-12-05T25:00", Precision = DatePrecision.Minute }).Should().BeNull();
    }

    [Fact]
    public void Map_ShouldNameEverySourceWithItsListingState_AndWhenTheSourceChangedATrackedEvent()
    {
        var at = new DateTimeOffset(2026, 11, 10, 8, 0, 0, TimeSpan.Zero);
        EventSourceModel Source(string id, string? name, EventSourceState? state, string? url) => new()
        {
            Kind = name is null ? EventSourceKind.Import : EventSourceKind.Scfd, Id = id, Name = name, State = state, Url = url,
            FirstSeenAtUtc = at, LastSeenAtUtc = at,
        };
        TrackedEventModel Tracked(DateTimeOffset updated, TrackingModel? tracking, params EventSourceModel[] sources) => new()
        {
            Event = new EventModel
            {
                Id = Guid.NewGuid(), Catalog = "ocharo-fashion", Facts = Facts("Jazz evening", "UZ", Day("2026-12-05")),
                Review = EventReviewState.Published, Sources = sources, Revision = 2, CreatedAtUtc = at, UpdatedAtUtc = updated,
            },
            Tracking = tracking,
        };
        TrackingModel Tracking(DateTimeOffset updated) => new()
        {
            Id = Guid.NewGuid(), EventId = Guid.NewGuid(), Stage = "shortlisted", Priority = TrackingPriority.Normal, Revision = 1,
            CreatedAtUtc = at, UpdatedAtUtc = updated,
        };

        ShowcaseEvent gone = EventsServiceMapper.ToShowcaseEvent(Tracked(at.AddDays(2), Tracking(at.AddDays(1)),
            Source("afisha/1", "Afisha Uzbekistan", EventSourceState.Gone, "https://afisha.example.test/jazz"),
            Source("anons/7", " ", EventSourceState.Unmatched, null),
            Source("desk/42", null, null, "https://desk.example.test/42")));
        ShowcaseEvent trackedLater = EventsServiceMapper.ToShowcaseEvent(Tracked(at.AddDays(1), Tracking(at.AddDays(2))));
        ShowcaseEvent untracked = EventsServiceMapper.ToShowcaseEvent(Tracked(at.AddDays(2), null,
            Source("afisha/2", "Afisha Uzbekistan", EventSourceState.Live, "https://afisha.example.test/show")));

        gone.Sources.Should().Equal(
            new ShowcaseEventSource("Afisha Uzbekistan", "https://afisha.example.test/jazz", "gone"),
            new ShowcaseEventSource(null, null, "unmatched"),
            new ShowcaseEventSource(null, "https://desk.example.test/42", null));
        gone.SourceUrl.Should().Be("https://afisha.example.test/jazz");
        gone.SourceUpdatedAt.Should().Be(at.AddDays(2));
        trackedLater.Sources.Should().BeEmpty();
        trackedLater.SourceUpdatedAt.Should().BeNull();
        untracked.Sources.Should().ContainSingle().Which.State.Should().Be("live");
        untracked.SourceUpdatedAt.Should().BeNull();
        EventsServiceMapper.SourceStateName(EventSourceState.Suspect).Should().Be("suspect");
    }

    [Fact]
    public async Task Summary_ShouldLeaveACancelledEventsDeadlineOutOfTheUpcomingOnes()
    {
        var service = new FakeEventsService();
        EventFacts Fair(string title, EventStatus status, string deadline) => Facts(title, "DE", Day("2027-06-10")) with
        {
            Status = status,
            Deadlines = [new EventDeadline { Kind = DeadlineKind.Application, At = Day(deadline) }],
        };
        service.Add(Fair("Open fair", EventStatus.Scheduled, "2027-05-01"));
        service.Add(Fair("Cancelled fair", EventStatus.Cancelled, "2027-04-01"));
        service.Add(Fair("Postponed fair", EventStatus.Postponed, "2027-05-02"));

        ShowcaseEventSummary summary = await Repository(service).GetSummaryAsync(Organization, new DateOnly(2027, 1, 1), 5, Token);

        summary.UpcomingDeadlines.Select(item => item.Title).Should().Equal("Open fair", "Postponed fair");
        summary.ByStatus.Should().Equal(new Dictionary<string, int> { ["candidate"] = 3 });
    }

    [Theory]
    [InlineData("uz", "uz-Latn")]
    [InlineData("uz-UZ", "uz-Latn")]
    [InlineData("uz-Cyrl", "uz-Cyrl")]
    [InlineData("ru", "ru-Cyrl")]
    [InlineData("de", "und")]
    public void Title_ShouldMatchByLanguagePrefix_ThenFallBackToUndeterminedEnglishAndAny(string tag, string expected)
    {
        var titles = new Dictionary<string, string>
        {
            ["und"] = "Undetermined", ["uz-Cyrl"] = "Узбекча", ["uz-Latn"] = "Uzbekcha", ["ru-Cyrl"] = "Русский",
        };

        EventsServiceMapper.Title(titles, [tag]).Language.Should().Be(expected);
    }

    [Fact]
    public void Title_ShouldFallBackToEnglishThenAnyTitle_AndNeverReturnBlank()
    {
        EventsServiceMapper.Title(new Dictionary<string, string> { ["en"] = "Fair", ["ru-Cyrl"] = "Ярмарка" }, ["de"])
            .Should().Be(("Fair", "en"));
        EventsServiceMapper.Title(new Dictionary<string, string> { ["ky-Cyrl"] = "Жарманке" }, ["uz"])
            .Should().Be(("Жарманке", "ky-Cyrl"));
        EventsServiceMapper.Title(new Dictionary<string, string> { ["ru-Cyrl"] = " " }, ["ru"])
            .Should().Be(("(untitled)", null));
    }

    [Theory]
    [InlineData("uz-UZ,ru;q=0.8,en;q=0.5,*;q=0.1", "uz-UZ,ru,en")]
    [InlineData("en;q=0.4, ru;q=0.9, uz", "uz,ru,en")]
    [InlineData("ru;q=0", "")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void AcceptLanguage_ShouldListTheTagsByWeight(string? header, string expected) =>
        string.Join(',', HttpUiLocale.Parse(header)).Should().Be(expected);

    [Fact]
    public async Task Update_ShouldAnswerConflictNotGone_WhenTheSourceRevisedTheFactsSinceThePageRead()
    {
        var service = new FakeEventsService();
        EventModel item = service.Add(Facts("Ambiente Frankfurt", "DE", Day("2027-01-29")));
        var repository = Repository(service);
        ShowcaseEvent shown = (await repository.GetEventsAsync(Organization, All, 200, 0, Token)).Single();
        service.Revise(item.Id, facts => facts with { Starts = Day("2027-02-05") });

        Result<ShowcaseEvent> stale = await repository.UpdateEventAsync(
            Organization, item.Id, shown.Revision, shown.EventRevision, InputOf(shown) with { Notes = "Ask for a stand." }, Token);

        stale.Should().BeOfType<Result<ShowcaseEvent>.Failure>().Which.Error.Type.Should().Be(AppErrorType.Conflict);
        service.TrackingWrites.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateTracking_ShouldRecordTheStage_AfterTheSourceRevisedTheFactsSinceThePageRead()
    {
        var service = new FakeEventsService();
        EventModel item = service.Add(Facts("Ambiente Frankfurt", "DE", Day("2027-01-29")));
        var repository = Repository(service);
        ShowcaseEvent shown = (await repository.GetEventsAsync(Organization, All, 200, 0, Token)).Single();
        service.Revise(item.Id, facts => facts with
        {
            Title = new Dictionary<string, string> { ["und"] = "Ambiente Frankfurt 2027" }, Starts = Day("2027-02-05"),
        });

        Result<ShowcaseEvent> saved = await repository.UpdateTrackingAsync(
            Organization, item.Id, shown.Revision, TrackingInput("shortlisted"), Token);

        ShowcaseEvent after = saved.Should().BeOfType<Result<ShowcaseEvent>.Success>().Which.Value;
        after.Status.Should().Be("shortlisted");
        after.Title.Should().Be("Ambiente Frankfurt 2027");
        after.EventRevision.Should().Be(shown.EventRevision + 1);
        after.Participation.Should().Be("Application package");
        after.Cost.Should().Be("USD 500");
        service.TrackingWrites.Should().ContainSingle().Which.ExpectedRevision.Should().BeNull();
    }

    [Fact]
    public async Task UpdateTracking_ShouldAnswerConflictBeforeEvents_ForAStaleTrackingRevision()
    {
        var service = new FakeEventsService();
        EventModel item = service.Add(Facts("Ambiente Frankfurt", "DE", Day("2027-01-29")));
        service.Track(item.Id, "applying");
        var repository = Repository(service);
        ShowcaseEvent shown = (await repository.GetEventsAsync(Organization, All, 200, 0, Token)).Single();

        Result<ShowcaseEvent> stale = await repository.UpdateTrackingAsync(
            Organization, item.Id, shown.Revision + 1, TrackingInput("shortlisted"), Token);
        Result<ShowcaseEvent> saved = await repository.UpdateTrackingAsync(
            Organization, item.Id, shown.Revision, TrackingInput("applied") with { Cost = null }, Token);

        stale.Should().BeOfType<Result<ShowcaseEvent>.Failure>().Which.Error.Type.Should().Be(AppErrorType.Conflict);
        saved.Should().BeOfType<Result<ShowcaseEvent>.Success>().Which.Value.Status.Should().Be("applied");
        service.TrackingWrites.Should().ContainSingle().Which.ExpectedRevision.Should().Be(1);
    }

    [Fact]
    public async Task UpdateTracking_ShouldKeepTheSourceLinkAnImportStoredBesideTheStage()
    {
        var service = new FakeEventsService();
        EventModel item = service.Add(Facts("Ambiente Frankfurt", "DE", Day("2027-01-29")));
        service.Track(item.Id, "shortlisted", fields: JsonSerializer.SerializeToElement(
            new { participation = "Old", cost = "Old cost", sourceUrl = "https://example.org/found" }));
        var repository = Repository(service);
        ShowcaseEvent shown = (await repository.GetEventsAsync(Organization, All, 200, 0, Token)).Single();

        Result<ShowcaseEvent> saved = await repository.UpdateTrackingAsync(
            Organization, item.Id, shown.Revision, TrackingInput("applying"), Token);

        ShowcaseEvent after = saved.Should().BeOfType<Result<ShowcaseEvent>.Success>().Which.Value;
        after.SourceUrl.Should().Be("https://example.org/found");
        after.Participation.Should().Be("Application package");
        after.Cost.Should().Be("USD 500");
    }

    [Fact]
    public async Task UpdateTracking_ShouldAnswerNotFound_ForAHiddenEvent()
    {
        var service = new FakeEventsService();
        EventModel hidden = service.Add(Facts("Rejected fair", "UZ", Day("2026-12-04")), EventReviewState.Rejected);

        Result<ShowcaseEvent> saved = await Repository(service).UpdateTrackingAsync(
            Organization, hidden.Id, 1, TrackingInput("shortlisted"), Token);

        saved.Should().BeOfType<Result<ShowcaseEvent>.Failure>().Which.Error.Type.Should().Be(AppErrorType.NotFound);
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

    /// <summary>OE05: an event Ocharo skipped or rejected leaves the feed unless it asks for closed events or names the
    /// stage, while the summary still counts it.</summary>
    [Fact]
    public async Task Read_ShouldLeaveSkippedAndRejectedEventsOut_UnlessAskedForThem_AndStillCountThem()
    {
        var service = new FakeEventsService();
        service.Add(Facts("Open fair", "UZ", Day("2027-06-01")));
        EventModel skipped = service.Add(Facts("Skipped fair", "UZ", Day("2027-06-02")));
        EventModel rejected = service.Add(Facts("Rejected fair", "UZ", Day("2027-06-03")));
        EventModel applying = service.Add(Facts("Applying fair", "UZ", Day("2027-06-04")));
        service.Track(skipped.Id, "skipped");
        service.Track(rejected.Id, "rejected");
        service.Track(applying.Id, "applying");
        var repository = Repository(service);
        ShowcaseEventFilter feed = All with { IncludeClosed = false };

        IReadOnlyList<ShowcaseEvent> shown = await repository.GetEventsAsync(Organization, feed, 200, 0, Token);
        IReadOnlyList<ShowcaseEvent> withClosed = await repository.GetEventsAsync(Organization, All, 200, 0, Token);
        IReadOnlyList<ShowcaseEvent> onlySkipped = await repository.GetEventsAsync(
            Organization, feed with { Status = "skipped" }, 200, 0, Token);
        ShowcaseEventSummary summary = await repository.GetSummaryAsync(Organization, new DateOnly(2027, 1, 1), 5, Token);

        shown.Select(item => item.Title).Should().Equal("Open fair", "Applying fair");
        withClosed.Select(item => item.Title).Should().Equal("Open fair", "Skipped fair", "Rejected fair", "Applying fair");
        onlySkipped.Select(item => item.Title).Should().Equal("Skipped fair");
        summary.ByStatus.Should().Equal(new Dictionary<string, int>
        {
            ["candidate"] = 1, ["skipped"] = 1, ["rejected"] = 1, ["applying"] = 1,
        });
    }

    [Fact]
    public void Validation_ShouldTakeEventsNameLimits()
    {
        var fields = new EventFields(
            "Ikat Week", new string('o', 180), "festival", "UZ", new string('c', 150), null, null, null,
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
        Func<Task> track = () => events.UpdateEventAsync(reader, item.Id, 1, null, Input("unknown", null), Token);
        Func<Task> trackOnly = () => events.UpdateTrackingAsync(reader, item.Id, 1, TrackingInput("shortlisted"), Token);

        (await propose.Should().ThrowAsync<AppException>()).Which.Error.Type.Should().Be(AppErrorType.Forbidden);
        (await track.Should().ThrowAsync<AppException>()).Which.Error.Type.Should().Be(AppErrorType.Forbidden);
        (await trackOnly.Should().ThrowAsync<AppException>()).Which.Error.Type.Should().Be(AppErrorType.Forbidden);
        service.TrackingWrites.Should().BeEmpty();
        (await events.GetEventsAsync(reader, null, null, null, null, null, null, true, false, 200, 0, Token)).Should().ContainSingle();
    }

    [Fact]
    public async Task Read_ShouldAnswerUnavailable_WhenEventsIsNotConfigured()
    {
        var repository = new EventsServiceRepository(
            new FakeEventsService(), new FakeEventsService(), Options.Create(new EventsServiceOptions()),
            new FixedUiLocale([]));

        Func<Task> read = () => repository.GetEventsAsync(Organization, All, 200, 0, Token);

        (await read.Should().ThrowAsync<AppException>()).Which.Error.Type.Should().Be(AppErrorType.ExternalUnavailable);
    }

    private static EventsServiceRepository Repository(FakeEventsService service, params string[] preferred) =>
        new(service, service, Options.Create(new EventsServiceOptions { BaseUrl = "https://events.example.test" }),
            new FixedUiLocale(preferred));

    private sealed class FixedUiLocale(IReadOnlyList<string> preferred) : IUiLocale
    {
        public IReadOnlyList<string> Preferred => preferred;
    }

    private static EventFacts Facts(string title, string? country, EventWhen starts) => new()
    {
        Title = new Dictionary<string, string> { ["und"] = title },
        Kind = "trade-fair",
        Place = country is null ? null : new EventPlace { Country = country },
        Starts = starts,
    };

    private static EventWhen Day(string value) => new() { Value = value, Precision = DatePrecision.Day };

    private static ShowcaseEventInput Input(string precision, DateOnly? startsOn) => new(
        "Ikat Week", "Ikat House", "festival", "UZ", "Margilan", null, startsOn, null, precision,
        startsOn?.AddDays(-30), "Application package", "Free", "https://example.org", "https://example.org/found",
        "candidate", "normal", "Bring scarves.");

    private static ShowcaseTrackingInput TrackingInput(string status) =>
        new(status, "normal", "Application package", "USD 500", "Ask for a stand.");

    private static ShowcaseEventInput InputOf(ShowcaseEvent item) => new(
        item.Title, item.Organizer, item.Format, item.Country, item.City, item.Venue, item.StartsOn,
        item.EndsOn, item.DatePrecision, item.ApplicationDeadline, item.Participation, item.Cost, item.Website,
        item.SourceUrl, item.Status, item.Priority, item.Notes);
}
