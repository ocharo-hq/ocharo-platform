using System.Text.Json;
using AwesomeAssertions;
using BrandWorkspace.Application.Events.Models;
using BrandWorkspace.Domain.Events.Models;
using BrandWorkspace.Infrastructure.Events.Services;
using Xunit;

namespace BrandWorkspace.Tests.Unit;

/// <summary>
/// Checks the WoW2 Events export (Events' contract § Imports) keeps ids, tracking and date uncertainty as Desk stored
/// them, and carries the whole row.
/// </summary>
public sealed class EventExportTests
{
    private static readonly Guid Organization = Guid.Parse("0d2a4c56-7a8e-4f3b-9c10-1b2c3d4e5f60");
    private static readonly DateTimeOffset At = new(2026, 10, 8, 9, 30, 0, TimeSpan.Zero);

    [Fact]
    public void Map_ShouldKeepEveryRow_KeyedByItsDeskId()
    {
        var first = Record(Event("first", "day", new DateOnly(2027, 1, 29)));
        var second = Record(Event(null, "unknown", null));

        EventExportBundle bundle = EventExportService.Map(Organization, [first, second]);

        bundle.Format.Should().Be("wow2.events-import.v1");
        bundle.Source.Should().Be("ocharo-desk");
        bundle.Rows.Select(row => row.LegacyId).Should().Equal(first.Event.Id.ToString(), second.Event.Id.ToString());
        bundle.Rows.Select(row => row.Facts.Key).Should().Equal("first", null);
    }

    [Theory]
    [InlineData("day", 2027, 1, 29, "2027-01-29", "day", "announced")]
    [InlineData("day", 2027, 3, 1, "2027-03-01", "day", "announced")]
    [InlineData("month", 2027, 5, 1, "2027-05", "month", "announced")]
    [InlineData("estimated", 2027, 4, 1, "2027-04", "month", "estimated")]
    public void When_ShouldKeepPrecision_AndNeverConfirmAnEstimate(
        string stored, int year, int month, int day, string value, string precision, string certainty)
    {
        EventExportService.When(new DateOnly(year, month, day), stored)
            .Should().Be(new ExportedWhen(value, precision, certainty));
    }

    [Fact]
    public void Map_ShouldGiveAnUnknownStartNoValue_AndNoEnd()
    {
        ExportedFacts facts = EventExportService.Map(Organization, [Record(Event(null, "unknown", null))])
            .Rows.Single().Facts;

        facts.Starts.Should().Be(new ExportedWhen(null, "unknown", "announced"));
        facts.Ends.Should().BeNull();
    }

    [Fact]
    public void Map_ShouldKeepTrackingAndTheWholeRow_WithTheTitleLanguageUndetermined()
    {
        ShowcaseEvent stored = Event("ambiente-frankfurt-2027-01", "estimated", new DateOnly(2027, 4, 1)) with
        {
            EndsOn = new DateOnly(2027, 4, 1),
            ApplicationDeadline = new DateOnly(2026, 12, 15),
            Status = "applying",
            Priority = "high",
            Notes = "Fit: ikat scarves.",
            Cost = "EUR 900 per stand",
            SourceUrl = "https://example.org/found",
            Revision = 3,
        };

        ExportedEventRow row = EventExportService.Map(Organization, [new ShowcaseEventRecord(stored, "user-7")])
            .Rows.Single();

        row.Facts.Title.Should().Equal(new Dictionary<string, string> { ["und"] = stored.Title });
        row.Facts.Kind.Should().Be("trade-fair");
        row.Facts.Organizer.Should().Be(new ExportedOrganizer("Messe Frankfurt", null));
        row.Facts.Place.Should().Be(new ExportedPlace(null, "Frankfurt", "DE"));
        row.Facts.Ends.Should().Be(new ExportedWhen("2027-04", "month", "estimated"));
        row.Facts.Deadlines.Should().Equal(
            new ExportedDeadline("application", new ExportedWhen("2026-12-15", "day", "announced")));
        row.SourceUrl.Should().Be("https://example.org/found");
        row.RecordedAtUtc.Should().Be(At);
        row.Tracking.Should().Be(new ExportedTracking(
            "applying", "high", "Fit: ikat scarves.",
            new ExportedTrackingFields(stored.Participation, "EUR 900 per stand")));
        row.Legacy.OrganizationId.Should().Be(Organization);
        row.Legacy.CreatedBy.Should().Be("user-7");
        row.Legacy.DatePrecision.Should().Be("estimated");
        row.Legacy.StartsOn.Should().Be(new DateOnly(2027, 4, 1));
        row.Legacy.Scope.Should().Be("international");
        row.Legacy.Revision.Should().Be(3);
    }

    [Fact]
    public void Bundle_ShouldSerializeInEventsWireShape()
    {
        EventExportBundle bundle = EventExportService.Map(
            Organization, [Record(Event("first", "month", new DateOnly(2027, 5, 1)))]);

        using JsonDocument json = JsonDocument.Parse(JsonSerializer.Serialize(bundle, JsonSerializerOptions.Web));
        JsonElement row = json.RootElement.GetProperty("rows")[0];

        json.RootElement.GetProperty("source").GetString().Should().Be("ocharo-desk");
        row.GetProperty("facts").GetProperty("title").GetProperty("und").GetString().Should().Be("Ambiente Frankfurt 2027");
        row.GetProperty("facts").GetProperty("starts").GetProperty("value").GetString().Should().Be("2027-05");
        row.GetProperty("tracking").GetProperty("fields").GetProperty("participation").GetString()
            .Should().Be("Stand booking");
        row.GetProperty("legacy").GetProperty("startsOn").GetString().Should().Be("2027-05-01");
    }

    [Fact]
    public async Task Export_ShouldRefuse_WhenTheBundleFileExists()
    {
        string file = Path.GetTempFileName();
        try
        {
            var exporter = new EventExportService(null!);
            Func<Task> act = () => exporter.ExportAsync(Organization, file, CancellationToken.None);

            (await act.Should().ThrowAsync<Exception>()).Which.Message.Should().Contain("already exists");
            (await File.ReadAllTextAsync(file)).Should().BeEmpty();
        }
        finally { File.Delete(file); }
    }

    private static ShowcaseEventRecord Record(ShowcaseEvent item) => new(item, "user-1");

    private static ShowcaseEvent Event(string? key, string precision, DateOnly? startsOn) => new(
        Guid.NewGuid(),
        key,
        "Ambiente Frankfurt 2027",
        "Messe Frankfurt",
        "international",
        "trade-fair",
        "DE",
        "Frankfurt",
        null,
        startsOn,
        null,
        precision,
        null,
        "Stand booking",
        null,
        "https://example.org",
        null,
        "candidate",
        "normal",
        string.Empty,
        1,
        At,
        At);
}
