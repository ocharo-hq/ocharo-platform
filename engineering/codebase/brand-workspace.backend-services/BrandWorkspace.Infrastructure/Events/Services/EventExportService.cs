using System.Globalization;
using System.Text.Json;
using BrandWorkspace.Application.Events.Models;
using BrandWorkspace.Application.Events.Repositories;
using BrandWorkspace.Application.Foundation.Errors;
using BrandWorkspace.Domain.Events.Models;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;

namespace BrandWorkspace.Infrastructure.Events.Services;

/// <summary>
/// Writes an organization's events as a WoW2 Events import bundle (Events' contract § Imports); it reads Desk and never
/// writes it (events-adoption.md #6).
/// </summary>
public sealed class EventExportService(IEventRepository repository)
{
    public const string Format = "wow2.events-import.v1";
    public const string Source = "ocharo-desk";

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
        EventExportBundle bundle = Map(organizationId, records);
        await using (var stream = new FileStream(bundlePath, FileMode.CreateNew, FileAccess.Write))
            await JsonSerializer.SerializeAsync(stream, bundle, BundleJson, token);
        return Report(organizationId, bundlePath, records);
    }

    /// <summary>Maps stored events to the bundle; precision and certainty carry over, never sharpen.</summary>
    public static EventExportBundle Map(Guid organizationId, IReadOnlyList<ShowcaseEventRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        return new EventExportBundle(Format, Source, records.Select(record => MapRow(organizationId, record)).ToList());
    }

    private static ExportedEventRow MapRow(Guid organizationId, ShowcaseEventRecord record)
    {
        ShowcaseEvent item = record.Event;
        var facts = new ExportedFacts(
            item.Key,
            new Dictionary<string, string>(StringComparer.Ordinal) { [UndeterminedLanguage] = item.Title },
            item.Format,
            item.Organizer is { } organizer ? new ExportedOrganizer(organizer, null) : null,
            item.Venue is null && item.City is null && item.Country is null
                ? null
                : new ExportedPlace(item.Venue, item.City, item.Country),
            When(item.StartsOn, item.DatePrecision) ?? new ExportedWhen(null, "unknown", Announced),
            When(item.EndsOn, item.DatePrecision),
            item.ApplicationDeadline is { } deadline
                ? [new ExportedDeadline("application", new ExportedWhen(Day(deadline), "day", Announced))]
                : [],
            item.Website);
        return new ExportedEventRow(
            item.Id.ToString(),
            facts,
            item.SourceUrl,
            item.CreatedAt,
            Legacy(organizationId, record),
            new ExportedTracking(
                item.Status, item.Priority, item.Notes, new ExportedTrackingFields(item.Participation, item.Cost)));
    }

    /// <summary>
    /// Maps a stored date: <c>day</c> stays a day; <c>month</c> and <c>estimated</c> become the month (<c>yyyy-MM</c>,
    /// never a first-of-month day), <c>estimated</c> marked so; no date maps to none.
    /// </summary>
    public static ExportedWhen? When(DateOnly? date, string precision)
    {
        if (date is not { } value) return null;
        return precision switch
        {
            "day" => new ExportedWhen(Day(value), "day", Announced),
            "month" => new ExportedWhen(Month(value), "month", Announced),
            "estimated" => new ExportedWhen(Month(value), "month", Estimated),
            _ => throw new InvalidOperationException($"A dated event cannot have precision '{precision}'."),
        };
    }

    private static string Day(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string Month(DateOnly date) => date.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    private static ShowcaseEventLegacyRow Legacy(Guid organizationId, ShowcaseEventRecord record)
    {
        ShowcaseEvent item = record.Event;
        return new ShowcaseEventLegacyRow(
            organizationId, item.Id, item.Key, item.Title, item.Organizer, item.Scope, item.Format, item.Country,
            item.City, item.Venue, item.StartsOn, item.EndsOn, item.DatePrecision, item.ApplicationDeadline,
            item.Participation, item.Cost, item.Website, item.SourceUrl, item.Status, item.Priority, item.Notes,
            item.Revision, record.CreatedBy, item.CreatedAt, item.UpdatedAt);
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
