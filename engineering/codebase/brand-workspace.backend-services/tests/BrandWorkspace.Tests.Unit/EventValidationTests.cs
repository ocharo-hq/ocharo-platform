using AwesomeAssertions;
using BrandWorkspace.Application.Preparation.Models;
using BrandWorkspace.Infrastructure.Events.Services;
using Xunit;

namespace BrandWorkspace.Tests.Unit;

/// <summary>Verifies showcase event normalization and date rules without I/O.</summary>
public sealed class EventValidationTests
{
    private readonly EventValidationService _validation = new();

    [Fact]
    public void Event_ShouldTrimBlankOptionalsToNull_UppercaseCountry_AndApplyDefaults()
    {
        var input = _validation.Event(Fields(
            title: "  Tashkent Fashion Week ", organizer: " ", country: " uz ", city: "", venue: "\t", startsOn: " 2026-10-20 ",
            cost: "  ", website: " https://example.test/tfw ", notes: null));
        input.Title.Should().Be("Tashkent Fashion Week");
        input.Organizer.Should().BeNull();
        input.Country.Should().Be("UZ");
        input.City.Should().BeNull();
        input.Venue.Should().BeNull();
        input.StartsOn.Should().Be(new DateOnly(2026, 10, 20));
        input.EndsOn.Should().BeNull();
        input.DatePrecision.Should().Be("day");
        input.Cost.Should().BeNull();
        input.Website.Should().Be("https://example.test/tfw");
        input.Participation.Should().BeEmpty();
        input.Notes.Should().BeEmpty();
        input.Status.Should().Be("shortlisted");
        input.Priority.Should().Be("normal");
    }

    [Fact]
    public void Event_ShouldDefaultToUnknownPrecision_WithoutDates_AndUseTheGivenDefaultStatus()
    {
        var input = _validation.Event(Fields(), EventValidationService.ImportedStatus);
        input.DatePrecision.Should().Be("unknown");
        input.StartsOn.Should().BeNull();
        input.Status.Should().Be("candidate");
        _validation.Event(Fields(status: "attended"), EventValidationService.ImportedStatus).Status.Should().Be("attended");
    }

    [Theory]
    [InlineData("2026-11-01", null, "month")]
    [InlineData("2026-11-01", "2026-11-30", "estimated")]
    [InlineData("2026-11-03", "2026-11-03", "day")]
    [InlineData(null, null, "unknown")]
    public void Event_ShouldAcceptConsistentDates(string? startsOn, string? endsOn, string precision)
    {
        var input = _validation.Event(Fields(startsOn: startsOn, endsOn: endsOn, datePrecision: precision));
        input.DatePrecision.Should().Be(precision);
        input.StartsOn.Should().Be(startsOn is null ? null : DateOnly.Parse(startsOn, System.Globalization.CultureInfo.InvariantCulture));
        input.EndsOn.Should().Be(endsOn is null ? null : DateOnly.Parse(endsOn, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData("2026-10-10", "2026-10-09", null)]
    [InlineData(null, "2026-10-09", null)]
    [InlineData("2026-10-10", null, "unknown")]
    [InlineData(null, null, "day")]
    [InlineData(null, null, "month")]
    [InlineData("2026-10-15", null, "month")]
    [InlineData("2026-10-15", null, "estimated")]
    [InlineData("2026-10-10", null, "weekly")]
    [InlineData("2026-02-30", null, null)]
    [InlineData("10/08/2026", null, null)]
    [InlineData("1999-12-31", null, null)]
    [InlineData("2101-01-01", null, null)]
    public void Event_ShouldRejectInconsistentDates(string? startsOn, string? endsOn, string? precision)
    {
        Action act = () => _validation.Event(Fields(startsOn: startsOn, endsOn: endsOn, datePrecision: precision));
        act.Should().Throw<PreparationException>().Which.Code.Should().Be("validation");
    }

    [Theory]
    [InlineData("scope", "galactic")]
    [InlineData("format", "gala")]
    [InlineData("status", "won")]
    [InlineData("priority", "urgent")]
    [InlineData("country", "UZB")]
    [InlineData("website", "javascript:alert(1)")]
    [InlineData("sourceUrl", "ftp://example.test/list")]
    [InlineData("deadline", "2026-13-01")]
    public void Event_ShouldRejectUnusableFields(string field, string value)
    {
        EventFields fields = field switch
        {
            "scope" => Fields() with { Scope = value },
            "format" => Fields() with { Format = value },
            "status" => Fields(status: value),
            "priority" => Fields() with { Priority = value },
            "country" => Fields(country: value),
            "website" => Fields(website: value),
            "sourceUrl" => Fields() with { SourceUrl = value },
            _ => Fields() with { ApplicationDeadline = value }
        };
        Action act = () => _validation.Event(fields);
        act.Should().Throw<PreparationException>().Which.Code.Should().Be("validation");
    }

    [Fact]
    public void Event_ShouldKeepMultilineCost_ButRejectMultilineTitle()
    {
        _validation.Event(Fields(cost: "Stand: 1 200 USD\nRegistration: 100 USD")).Cost.Should().Be("Stand: 1 200 USD\nRegistration: 100 USD");
        Action act = () => _validation.Event(Fields(title: "Two\nlines"));
        act.Should().Throw<PreparationException>();
    }

    [Fact]
    public void Filter_ShouldTreatBlankFiltersAsAbsent_AndParseDates()
    {
        var filter = _validation.Filter(" ", "", null, " ", "2026-10-01", null, includeUndated: false);
        filter.Query.Should().BeNull();
        filter.Scope.Should().BeNull();
        filter.Format.Should().BeNull();
        filter.Status.Should().BeNull();
        filter.From.Should().Be(new DateOnly(2026, 10, 1));
        filter.To.Should().BeNull();
        filter.IncludeUndated.Should().BeFalse();
        Action act = () => _validation.Filter(null, "galactic", null, null, null, null, true);
        act.Should().Throw<PreparationException>();
    }

    [Fact]
    public void Key_ShouldAcceptOnlyLowercaseSlugs()
    {
        _validation.Key("tfw-2026").Should().Be("tfw-2026");
        Action act = () => _validation.Key("TFW 2026");
        act.Should().Throw<PreparationException>();
    }

    private static EventFields Fields(
        string? title = "Event", string? organizer = null, string? country = null, string? city = null, string? venue = null,
        string? startsOn = null, string? endsOn = null, string? datePrecision = null, string? cost = null, string? website = null,
        string? status = null, string? notes = null) =>
        new(title, organizer, "local", "runway", country, city, venue, startsOn, endsOn, datePrecision, null, null, cost, website,
            null, status, null, notes);
}
