using AwesomeAssertions;
using BrandWorkspace.Application.Events.Models;
using BrandWorkspace.Domain.Events.Models;
using BrandWorkspace.Infrastructure.Events.Services;
using Xunit;

namespace BrandWorkspace.Tests.Unit;

/// <summary>Checks the WoW2 Events export keeps ids, tracking, provenance and date uncertainty as Desk stored them.</summary>
public sealed class EventExportTests
{
    private static readonly Guid Organization = Guid.Parse("0d2a4c56-7a8e-4f3b-9c10-1b2c3d4e5f60");
    private static readonly DateTimeOffset At = new(2026, 10, 8, 9, 30, 0, TimeSpan.Zero);

    [Fact]
    public void Map_ShouldKeepEveryRow_KeyedByItsDeskId()
    {
        var first = Record(Event("first", "day", new DateOnly(2027, 1, 29)));
        var second = Record(Event(null, "unknown", null));

        EventExportBundle bundle = EventExportService.Map(Organization, [first, second], At);

        bundle.Format.Should().Be("wow2.events-import.v1");
        bundle.Source.Should().Be(new EventExportSource("ocharo-desk", Organization, At, 2));
        bundle.Events.Select(item => item.SourceId).Should().Equal(first.Event.Id, second.Event.Id);
        bundle.Events.Select(item => item.Key).Should().Equal("first", null);
    }

    [Theory]
    [InlineData("day", 2027, 1, 29, "day", "announced")]
    [InlineData("day", 2027, 3, 1, "day", "announced")]
    [InlineData("month", 2027, 5, 1, "month", "announced")]
    [InlineData("estimated", 2027, 4, 1, "month", "estimated")]
    public void Date_ShouldKeepPrecision_AndNeverConfirmAnEstimate(
        string stored, int year, int month, int day, string precision, string certainty)
    {
        var date = new DateOnly(year, month, day);

        EventExportService.Date(date, stored).Should().Be(new ExportedDate(date, precision, certainty));
    }

    [Fact]
    public void Map_ShouldStoreNoDate_WhenThePrecisionIsUnknown()
    {
        ExportedEvent exported = EventExportService.Map(Organization, [Record(Event(null, "unknown", null))], At)
            .Events.Single();

        exported.Starts.Should().BeNull();
        exported.Ends.Should().BeNull();
    }

    [Fact]
    public void Map_ShouldKeepTrackingProvenanceAndTheStoredRow_WithTheTitleLanguageUndetermined()
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

        ExportedEvent exported = EventExportService.Map(Organization, [new ShowcaseEventRecord(stored, "user-7")], At)
            .Events.Single();

        exported.Title.Should().Be(new ExportedText(stored.Title, "und"));
        exported.Ends.Should().Be(new ExportedDate(new DateOnly(2027, 4, 1), "month", "estimated"));
        exported.Deadlines.Should().Equal(new ExportedDeadline("application", new DateOnly(2026, 12, 15)));
        exported.Tracking.Should().Be(
            new ExportedTracking("applying", "high", "Fit: ikat scarves.", stored.Participation, "EUR 900 per stand"));
        exported.Provenance.Should().Be(new ExportedProvenance(
            "ocharo-desk", Organization, stored.Id, stored.Key, "https://example.org/found", "user-7", At, At, 3));
        exported.Legacy.Should().Be(stored);
    }

    [Fact]
    public async Task Export_ShouldRefuse_WhenTheBundleFileExists()
    {
        string file = Path.GetTempFileName();
        try
        {
            var exporter = new EventExportService(null!, TimeProvider.System);
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
