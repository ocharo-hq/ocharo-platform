using System.Globalization;
using System.Text.Json;
using BrandWorkspace.Application.Events.Models;
using BrandWorkspace.Domain.Events.Models;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Tracking;

namespace BrandWorkspace.Infrastructure.Events.Services;

/// <summary>
/// Maps WoW2 Events' catalog and tracking onto Desk's <see cref="ShowcaseEvent"/> contract and back
/// (events-adoption.md #8), so Desk's routes and screens keep their shape while Events stores the records.
/// </summary>
/// <remarks>
/// Desk's revision is the tracking revision plus one: an event Ocharo never tracked reads as revision 1, which Desk's
/// screens require to be positive, and a write at revision 1 creates its tracking.
/// </remarks>
public static class EventsServiceMapper
{
    public const string UntrackedStatus = "candidate";
    private const string ApplicationDeadline = "application";
    private static readonly string[] TitleLocales = ["und", "en", "uz", "ru"];

    /// <summary>Holds the countries Desk counts as regional to Ocharo in Uzbekistan; the 68 stored scopes agree.</summary>
    private static readonly HashSet<string> RegionalCountries = new(StringComparer.Ordinal)
    {
        "KZ", "KG", "TJ", "TM", "AF", "AZ", "RU",
    };

    /// <summary>Tells whether Desk's feed shows the event: published or a candidate, never merged away.</summary>
    public static bool IsVisible(EventModel item) =>
        item.MergedInto is null && item.Review is EventReviewState.Published or EventReviewState.Candidate;

    /// <summary>Maps an event and this organization's tracking of it to Desk's record.</summary>
    public static ShowcaseEvent ToShowcaseEvent(TrackedEventModel tracked)
    {
        ArgumentNullException.ThrowIfNull(tracked);
        EventModel item = tracked.Event;
        EventFacts facts = item.Facts;
        TrackingModel? tracking = tracked.Tracking;
        (DateOnly? startsOn, string precision) = Start(facts.Starts);
        DateOnly? endsOn = startsOn is { } start && Date(facts.Ends) is { } end && end >= start ? end : null;
        JsonElement? fields = tracking?.Fields;
        return new ShowcaseEvent(
            item.Id,
            facts.Key,
            Title(facts.Title),
            Blank(facts.Organizer?.Name),
            Scope(facts.Place?.Country),
            EventValidationService.Formats.Contains(facts.Kind) ? facts.Kind : "other",
            Blank(facts.Place?.Country),
            Blank(facts.Place?.City),
            Blank(facts.Place?.Venue),
            startsOn,
            endsOn,
            precision,
            Date(facts.Deadlines.FirstOrDefault(IsApplication)?.At),
            Field(fields, "participation") ?? string.Empty,
            Blank(Field(fields, "cost")),
            Blank(facts.Website),
            Blank(item.Sources.Select(source => source.Url).FirstOrDefault(url => url is not null) ?? Field(fields, "sourceUrl")),
            tracking is not null && EventValidationService.Statuses.Contains(tracking.Stage) ? tracking.Stage : UntrackedStatus,
            Priority(tracking?.Priority ?? TrackingPriority.Normal),
            tracking?.Notes ?? string.Empty,
            DeskRevision(tracking),
            tracking?.CreatedAtUtc ?? item.CreatedAtUtc,
            tracking is { } own && own.UpdatedAtUtc > item.UpdatedAtUtc ? own.UpdatedAtUtc : item.UpdatedAtUtc);
    }

    /// <summary>Gets Desk's revision for a tracking: its revision plus one, or 1 when untracked.</summary>
    public static int DeskRevision(TrackingModel? tracking) => (tracking?.Revision ?? 0) + 1;

    /// <summary>Gets the tracking revision a Desk write expects; null when Desk's revision says untracked.</summary>
    public static int? TrackingRevision(int deskRevision) => deskRevision > 1 ? deskRevision - 1 : null;

    /// <summary>Maps Desk's input to the facts of a member proposal; the title's language is undetermined.</summary>
    public static EventFacts ToFacts(ShowcaseEventInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return new EventFacts
        {
            Title = new Dictionary<string, string>(StringComparer.Ordinal) { ["und"] = input.Title },
            Kind = input.Format,
            Organizer = input.Organizer is { } organizer ? new EventOrganizer { Name = organizer } : null,
            Place = input.Venue is null && input.City is null && input.Country is null
                ? null
                : new EventPlace { Venue = input.Venue, City = input.City, Country = input.Country },
            Starts = When(input.StartsOn, input.DatePrecision) ?? new EventWhen(),
            Ends = When(input.EndsOn, input.DatePrecision),
            Deadlines = input.ApplicationDeadline is { } deadline
                ? [new EventDeadline { Kind = DeadlineKind.Application, At = When(deadline, "day")! }]
                : [],
            Website = input.Website,
        };
    }

    /// <summary>Maps Desk's input to this organization's tracking at the tracking revision it read.</summary>
    public static TrackingSpec ToTracking(ShowcaseEventInput input, int? expectedRevision)
    {
        ArgumentNullException.ThrowIfNull(input);
        return new TrackingSpec
        {
            Stage = input.Status,
            Priority = input.Priority switch
            {
                "high" => TrackingPriority.High,
                "low" => TrackingPriority.Low,
                _ => TrackingPriority.Normal,
            },
            Notes = input.Notes,
            Fields = JsonSerializer.SerializeToElement(
                new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["participation"] = input.Participation,
                    ["cost"] = input.Cost,
                    ["sourceUrl"] = input.SourceUrl,
                }),
            ExpectedRevision = expectedRevision,
        };
    }

    /// <summary>
    /// Tells whether Desk's input keeps the event's facts as Events serves them; scope is derived, so it never counts.
    /// </summary>
    public static bool HasSameFacts(ShowcaseEvent current, ShowcaseEventInput input)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(input);
        return current.Title == input.Title && current.Organizer == input.Organizer && current.Format == input.Format
            && current.Country == input.Country && current.City == input.City && current.Venue == input.Venue
            && current.StartsOn == input.StartsOn && current.EndsOn == input.EndsOn
            && current.DatePrecision == input.DatePrecision && current.ApplicationDeadline == input.ApplicationDeadline
            && current.Website == input.Website && current.SourceUrl == input.SourceUrl;
    }

    /// <summary>Derives Desk's scope from the country: Uzbekistan is local, its neighbours regional, the rest international.</summary>
    public static string Scope(string? country) => country switch
    {
        "UZ" => "local",
        { } code when RegionalCountries.Contains(code) => "regional",
        _ => "international",
    };

    private static (DateOnly? Date, string Precision) Start(EventWhen when) => when.Precision switch
    {
        DatePrecision.Day or DatePrecision.Minute => (Day(when.Value), "day"),
        DatePrecision.Month => (Month(when.Value), when.Certainty == DateCertainty.Estimated ? "estimated" : "month"),
        _ => (null, "unknown"),
    } is ({ } date, var precision) ? (date, precision) : (null, "unknown");

    private static DateOnly? Date(EventWhen? when) => when?.Precision switch
    {
        DatePrecision.Day or DatePrecision.Minute => Day(when.Value),
        DatePrecision.Month => Month(when.Value),
        _ => null,
    };

    private static EventWhen? When(DateOnly? date, string precision)
    {
        if (date is not { } value) return null;
        return precision switch
        {
            "day" => new EventWhen { Value = value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Precision = DatePrecision.Day },
            "month" => new EventWhen { Value = value.ToString("yyyy-MM", CultureInfo.InvariantCulture), Precision = DatePrecision.Month },
            "estimated" => new EventWhen
            {
                Value = value.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                Precision = DatePrecision.Month,
                Certainty = DateCertainty.Estimated,
            },
            _ => null,
        };
    }

    private static DateOnly? Day(string? value) =>
        value is { Length: >= 10 } && DateOnly.TryParseExact(value[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var day) ? day : null;

    private static DateOnly? Month(string? value) =>
        value is { Length: >= 7 } && DateOnly.TryParseExact(value[..7] + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var month) ? month : null;

    private static bool IsApplication(EventDeadline deadline) => deadline.Kind == DeadlineKind.Application;

    private static string Title(IReadOnlyDictionary<string, string> titles)
    {
        foreach (var locale in TitleLocales)
            if (titles.TryGetValue(locale, out var title) && !string.IsNullOrWhiteSpace(title)) return title;
        return titles.Values.FirstOrDefault(title => !string.IsNullOrWhiteSpace(title)) ?? "(untitled)";
    }

    private static string Priority(TrackingPriority priority) => priority switch
    {
        TrackingPriority.High => "high",
        TrackingPriority.Low => "low",
        _ => "normal",
    };

    private static string? Field(JsonElement? fields, string name) =>
        fields is { ValueKind: JsonValueKind.Object } value && value.TryGetProperty(name, out var field)
            && field.ValueKind == JsonValueKind.String ? field.GetString() : null;

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
