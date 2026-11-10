using System.Text.Json;
using BrandWorkspace.Application.Events.Models;
using BrandWorkspace.Application.Events.Repositories;
using BrandWorkspace.Application.Foundation.Errors;
using BrandWorkspace.Domain.Events.Models;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;

namespace BrandWorkspace.Infrastructure.Events.Services;

/// <summary>
/// Writes an organization's events as a WoW2 Events import bundle; it reads Desk and never writes it
/// (events-adoption.md #6).
/// </summary>
public sealed class EventExportService(IEventRepository repository, TimeProvider time)
{
    public const string Format = "wow2.events-import.v1";
    public const string SourceSystem = "ocharo-desk";

    /// <summary>Holds the BCP 47 tag for a language nobody recorded; no title is assumed Uzbek.</summary>
    public const string UndeterminedLanguage = "und";

    public const string Announced = "announced";
    public const string Estimated = "estimated";

    private static readonly JsonSerializerOptions BundleJson = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    /// <summary>
    /// Exports every event of <paramref name="organizationId"/> to a new file at <paramref name="bundlePath"/>; an
    /// existing file refuses, so an earlier export is never overwritten.
    /// </summary>
    public async Task<EventExportReport> ExportAsync(Guid organizationId, string bundlePath, CancellationToken token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bundlePath);
        if (File.Exists(bundlePath))
            throw DeskErrors.Conflict("The bundle file already exists; choose a new path.").ToException();
        IReadOnlyList<ShowcaseEventRecord> records = await repository.GetEventRecordsAsync(organizationId, token);
        EventExportBundle bundle = Map(organizationId, records, time.GetUtcNow());
        await using (var stream = new FileStream(bundlePath, FileMode.CreateNew, FileAccess.Write))
            await JsonSerializer.SerializeAsync(stream, bundle, BundleJson, token);
        return Report(organizationId, bundlePath, records);
    }

    /// <summary>Maps stored events to the bundle; precision and certainty carry over, never sharpen.</summary>
    public static EventExportBundle Map(
        Guid organizationId, IReadOnlyList<ShowcaseEventRecord> records, DateTimeOffset exportedAt)
    {
        ArgumentNullException.ThrowIfNull(records);
        var events = records.Select(record => MapEvent(organizationId, record)).ToList();
        return new EventExportBundle(
            Format, new EventExportSource(SourceSystem, organizationId, exportedAt, events.Count), events);
    }

    private static ExportedEvent MapEvent(Guid organizationId, ShowcaseEventRecord record)
    {
        ShowcaseEvent item = record.Event;
        return new ExportedEvent(
            item.Id,
            item.Key,
            new ExportedText(item.Title, UndeterminedLanguage),
            item.Organizer,
            item.Format,
            new ExportedPlace(item.Country, item.City, item.Venue),
            Date(item.StartsOn, item.DatePrecision),
            Date(item.EndsOn, item.DatePrecision),
            item.ApplicationDeadline is { } deadline ? [new ExportedDeadline("application", deadline)] : [],
            item.Website,
            new ExportedTracking(item.Status, item.Priority, item.Notes, item.Participation, item.Cost),
            new ExportedProvenance(
                SourceSystem,
                organizationId,
                item.Id,
                item.Key,
                item.SourceUrl,
                record.CreatedBy,
                item.CreatedAt,
                item.UpdatedAt,
                item.Revision),
            item);
    }

    /// <summary>
    /// Maps a stored date: <c>day</c> stays a day; <c>month</c> and <c>estimated</c> keep the 1st of the month at month
    /// precision, <c>estimated</c> marked so; <c>unknown</c> stores no date.
    /// </summary>
    public static ExportedDate? Date(DateOnly? date, string precision)
    {
        if (date is not { } value) return null;
        return precision switch
        {
            "day" => new ExportedDate(value, "day", Announced),
            "month" => new ExportedDate(value, "month", Announced),
            "estimated" => new ExportedDate(value, "month", Estimated),
            _ => throw new InvalidOperationException($"A dated event cannot have precision '{precision}'."),
        };
    }

    private static EventExportReport Report(
        Guid organizationId, string bundlePath, IReadOnlyList<ShowcaseEventRecord> records)
    {
        return new EventExportReport(
            organizationId,
            bundlePath,
            records.Count,
            records.Count(record => record.Event.Key is not null),
            Count(records, record => record.Event.Status),
            Count(records, record => record.Event.DatePrecision));
    }

    private static IReadOnlyDictionary<string, int> Count(
        IReadOnlyList<ShowcaseEventRecord> records, Func<ShowcaseEventRecord, string> by)
    {
        return records.GroupBy(by, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
    }
}
