using OcharoEvents.Api.Events.Models;
using OcharoEvents.Infrastructure.Events.Services;
using WoW.Two.Sdk.Beta.Be.Events;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Tracking;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;

namespace OcharoEvents.Tests.Unit;

/// <summary>How the Events page's mirror reads the kit's query into WoW2 Events' own (OE07): every filter crosses as
/// Events defines it, defaults as Events' routes read them, and a malformed day or wire name is refused here.</summary>
public sealed class EventsQueryParametersTests
{
    [Fact]
    public void TrackingQuery_ShouldCarryEveryFilter_AsEventsDefinesIt()
    {
        var parameters = new TrackingQueryParameters
        {
            TrackedOnly = true,
            Stage = "applying",
            Stages = ["shortlisted", "confirmed"],
            ExcludeStages = ["skipped", "rejected"],
            Priority = "high",
            From = "2026-11-19",
            To = "2026-12-31",
            Kind = "trade-fair",
            Country = "UZ",
            Countries = ["KZ", "KG"],
            ExcludeCountries = ["RU"],
            IncludeUnknownCountry = true,
            Text = "silk",
            DeadlineBefore = "2026-12-01",
            IncludeUndated = false,
            Cursor = "page-2",
            Limit = 25,
        };

        parameters.TryMap(out var query).Should().BeNull();

        query.Should().BeEquivalentTo(new TrackingQuery
        {
            TrackedOnly = true,
            Stage = "applying",
            Stages = ["shortlisted", "confirmed"],
            ExcludeStages = ["skipped", "rejected"],
            Priority = TrackingPriority.High,
            From = new DateOnly(2026, 11, 19),
            To = new DateOnly(2026, 12, 31),
            Kind = "trade-fair",
            Country = "UZ",
            Countries = ["KZ", "KG"],
            ExcludeCountries = ["RU"],
            IncludeUnknownCountry = true,
            Text = "silk",
            DeadlineBefore = new DateOnly(2026, 12, 1),
            IncludeUndated = false,
            Cursor = "page-2",
            Limit = 25,
        });
    }

    [Fact]
    public void TrackingQuery_ShouldReadAnEmptyQuery_AsEventsRoutesDo()
    {
        new TrackingQueryParameters().TryMap(out var query).Should().BeNull();

        query.TrackedOnly.Should().BeFalse();
        query.Stages.Should().BeEmpty();
        query.ExcludeStages.Should().BeEmpty();
        query.Countries.Should().BeEmpty();
        query.IncludeUnknownCountry.Should().BeFalse();
        query.IncludeUndated.Should().BeTrue();
        query.Limit.Should().Be(EventsConstants.DefaultPageSize);
    }

    [Theory]
    [InlineData("from", "03.12.2026")]
    [InlineData("to", "2026-13-01")]
    [InlineData("deadlineBefore", "soon")]
    [InlineData("priority", "urgent")]
    public void TrackingQuery_ShouldRefuseAMalformedValue(string name, string value)
    {
        var parameters = name switch
        {
            "from" => new TrackingQueryParameters { From = value },
            "to" => new TrackingQueryParameters { To = value },
            "deadlineBefore" => new TrackingQueryParameters { DeadlineBefore = value },
            _ => new TrackingQueryParameters { Priority = value },
        };

        var error = parameters.TryMap(out _);

        error.Should().NotBeNull();
        error!.Type.Should().Be(AppErrorType.Validation);
        error.Message.Should().StartWith(name);
    }

    [Fact]
    public void EventQuery_ShouldCarryTheCalendarFilters_ByTheirWireNames()
    {
        var parameters = new EventQueryParameters
        {
            From = "2026-11-19", Kind = "runway", Country = "UZ", Text = "week", Status = "movedOnline", Review = "published",
        };

        parameters.TryMap(out var query).Should().BeNull();

        query.From.Should().Be(new DateOnly(2026, 11, 19));
        query.Kind.Should().Be("runway");
        query.Country.Should().Be("UZ");
        query.Text.Should().Be("week");
        query.Status.Should().Be(EventStatus.MovedOnline);
        query.Review.Should().Be(EventReviewState.Published);
        query.IncludeUndated.Should().BeTrue();
        new EventQueryParameters { Review = "maybe" }.TryMap(out _).Should().NotBeNull();
    }

    [Fact]
    public void CatalogGuard_ShouldServeOnlyTheConfiguredCatalog_OfAConfiguredEvents()
    {
        var configured = new EventsServiceOptions { BaseUrl = "https://events.example.test/" };

        EventsCatalogGuard.Refusal(configured, "ocharo-fashion").Should().BeNull();
        EventsCatalogGuard.Refusal(configured).Should().BeNull();
        EventsCatalogGuard.Refusal(configured, "another")!.Type.Should().Be(AppErrorType.NotFound);
        EventsCatalogGuard.Refusal(new EventsServiceOptions(), "ocharo-fashion")!.Type.Should().Be(AppErrorType.ExternalUnavailable);
    }
}
