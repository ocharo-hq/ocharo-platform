using System.Globalization;
using System.Text.Json;
using OcharoEvents.Application.Events.Models;
using OcharoEvents.Domain.Events.Models;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Tracking;

namespace OcharoEvents.Infrastructure.Events.Services;

/// <summary>
/// Maps WoW2 Events' catalog and tracking onto the Events page's <see cref="ShowcaseEvent"/> contract and back
/// (events-adoption.md #8; events-app.md #3), so the page keeps the wire it had in Desk while Events stores the records.
/// </summary>
/// <remarks>
/// The page's revision is the tracking revision plus one: an event Ocharo never tracked reads as revision 1, which the
/// page requires to be positive, and a write at revision 1 creates its tracking.
/// </remarks>
public static class EventsServiceMapper
{
    public const string UntrackedStatus = "candidate";
    private const string ApplicationDeadline = "application";
    /// <summary>Holds the title languages tried after the caller's own: undetermined, English, then Ocharo's two operator
    /// languages, so an event with no title in the caller's language still reads the same way every time.</summary>
    private static readonly string[] FallbackTitleLanguages = ["und", "en", "uz", "ru"];

    /// <summary>Holds the countries the page counts as regional to Ocharo in Uzbekistan; the 68 stored scopes agree.</summary>
    private static readonly HashSet<string> RegionalCountries = new(StringComparer.Ordinal)
    {
        "KZ", "KG", "TJ", "TM", "AF", "AZ", "RU",
    };

    /// <summary>Tells whether the page's feed shows the event: published or a candidate, never merged away.</summary>
    public static bool IsVisible(EventModel item) =>
        item.MergedInto is null && item.Review is EventReviewState.Published or EventReviewState.Candidate;

    /// <summary>Maps an event and this organization's tracking of it to the page's record; the title follows
    /// <paramref name="preferred"/>, the caller's language tags, most preferred first.</summary>
    public static ShowcaseEvent ToShowcaseEvent(TrackedEventModel tracked, IReadOnlyList<string>? preferred = null)
    {
        ArgumentNullException.ThrowIfNull(tracked);
        EventModel item = tracked.Event;
        EventFacts facts = item.Facts;
        TrackingModel? tracking = tracked.Tracking;
        (DateOnly? startsOn, string precision) = Start(facts.Starts);
        DateOnly? endsOn = startsOn is { } start && Date(facts.Ends) is { } end && end >= start ? end : null;
        JsonElement? fields = tracking?.Fields;
        EventOffer? offer = TicketOffer(facts.Offers);
        (string title, string? titleLanguage) = Title(facts.Title, preferred ?? []);
        return new ShowcaseEvent(
            item.Id,
            facts.Key,
            title,
            titleLanguage,
            facts.Languages.Where(language => !string.IsNullOrWhiteSpace(language)).ToList(),
            Blank(facts.Organizer?.Name),
            Blank(facts.Organizer?.Website),
            Scope(facts.Place?.Country),
            EventValidationService.Formats.Contains(facts.Kind) ? facts.Kind : "other",
            EventStatusName(facts.Status),
            AttendanceName(facts.Attendance),
            Blank(facts.Place?.Country),
            Blank(facts.Place?.City),
            Blank(facts.Place?.Venue),
            Blank(facts.Place?.Address),
            startsOn,
            endsOn,
            precision,
            startsOn is null ? null : StartTime(facts.Starts),
            Blank(facts.TimeZone),
            Date(facts.Deadlines.FirstOrDefault(IsApplication)?.At),
            Field(fields, "participation") ?? string.Empty,
            Blank(Field(fields, "cost")),
            offer is null ? null : new ShowcaseTickets(offer.MinPrice, offer.MaxPrice, Blank(offer.Currency), offer.Free),
            Blank(facts.TicketUrl) ?? (offer is { Kind: OfferKind.Ticket } ? Blank(offer.Url) : null),
            Blank(facts.Website),
            Blank(item.Sources.Select(source => source.Url).FirstOrDefault(url => url is not null) ?? Field(fields, "sourceUrl")),
            item.Sources.Select(source => new ShowcaseEventSource(Blank(source.Name), Blank(source.Url), SourceStateName(source.State)))
                .ToList(),
            tracking is not null && item.UpdatedAtUtc > tracking.UpdatedAtUtc ? item.UpdatedAtUtc : null,
            tracking is not null && EventValidationService.Statuses.Contains(tracking.Stage) ? tracking.Stage : UntrackedStatus,
            Priority(tracking?.Priority ?? TrackingPriority.Normal),
            tracking?.Notes ?? string.Empty,
            ShownRevision(tracking),
            item.Revision,
            tracking?.CreatedAtUtc ?? item.CreatedAtUtc,
            tracking is { } own && own.UpdatedAtUtc > item.UpdatedAtUtc ? own.UpdatedAtUtc : item.UpdatedAtUtc);
    }

    /// <summary>Gets the page's revision for a tracking: its revision plus one, or 1 when untracked.</summary>
    public static int ShownRevision(TrackingModel? tracking) => (tracking?.Revision ?? 0) + 1;

    /// <summary>Gets the tracking revision a page write expects; null when the page's revision says untracked.</summary>
    public static int? TrackingRevision(int shownRevision) => shownRevision > 1 ? shownRevision - 1 : null;

    /// <summary>Maps the page's input to the facts of a member proposal; the title's language is undetermined.</summary>
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

    /// <summary>Maps the page's input to this organization's tracking at the tracking revision it read.</summary>
    public static TrackingSpec ToTracking(ShowcaseEventInput input, int? expectedRevision)
    {
        ArgumentNullException.ThrowIfNull(input);
        return new TrackingSpec
        {
            Stage = input.Status,
            Priority = TrackingPriorityOf(input.Priority),
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
    /// Maps the tracking fields alone to this organization's tracking at the tracking revision the page read. The tracking's
    /// other stored fields, such as the source link an import kept there, stay as they were.
    /// </summary>
    public static TrackingSpec ToTracking(ShowcaseTrackingInput input, JsonElement? storedFields, int? expectedRevision)
    {
        ArgumentNullException.ThrowIfNull(input);
        var fields = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (storedFields is { ValueKind: JsonValueKind.Object } stored)
            foreach (JsonProperty property in stored.EnumerateObject())
                fields[property.Name] = property.Value;
        fields["participation"] = input.Participation;
        fields["cost"] = input.Cost;
        return new TrackingSpec
        {
            Stage = input.Status,
            Priority = TrackingPriorityOf(input.Priority),
            Notes = input.Notes,
            Fields = JsonSerializer.SerializeToElement(fields),
            ExpectedRevision = expectedRevision,
        };
    }

    /// <summary>
    /// Tells whether the page's input keeps the event's facts as Events serves them; scope is derived, so it never counts.
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

    /// <summary>Derives the page's scope from the country: Uzbekistan is local, its neighbours regional, the rest international.</summary>
    public static string Scope(string? country) => country switch
    {
        "UZ" => "local",
        { } code when RegionalCountries.Contains(code) => "regional",
        _ => "international",
    };

    // An estimated start (SCFD's AI estimate, or an import's) never reads as exact: the page has no estimated day, so an
    // estimate at day or minute precision shows as its estimated month.
    private static (DateOnly? Date, string Precision) Start(EventWhen when) => when.Precision switch
    {
        DatePrecision.Day or DatePrecision.Minute when when.Certainty == DateCertainty.Estimated => (Month(when.Value), "estimated"),
        DatePrecision.Day or DatePrecision.Minute => (Day(when.Value), "day"),
        DatePrecision.Month => (Month(when.Value), when.Certainty == DateCertainty.Estimated ? "estimated" : "month"),
        _ => (null, "unknown"),
    } is ({ } date, var precision) ? (date, precision) : (null, "unknown");

    /// <summary>Reads the local start time (<c>HH:mm</c>) of a start announced to the minute; null for any coarser or estimated start.</summary>
    public static string? StartTime(EventWhen when) =>
        when is { Precision: DatePrecision.Minute, Certainty: not DateCertainty.Estimated } && when.Value is { Length: >= 16 } value && value[10] is 'T' or ' '
            && TimeOnly.TryParseExact(value[11..16], "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)
            ? time.ToString("HH:mm", CultureInfo.InvariantCulture)
            : null;

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

    /// <summary>Picks the offer that tells what attending costs: the first ticket offer, else the first offer; null without
    /// offers.</summary>
    public static EventOffer? TicketOffer(IReadOnlyList<EventOffer> offers) =>
        offers.FirstOrDefault(offer => offer.Kind == OfferKind.Ticket) ?? offers.FirstOrDefault();

    /// <summary>Names whether the event still happens as listed, in the page's wire words.</summary>
    public static string EventStatusName(EventStatus status) => status switch
    {
        EventStatus.Cancelled => "cancelled",
        EventStatus.Postponed => "postponed",
        EventStatus.Rescheduled => "rescheduled",
        EventStatus.MovedOnline => "moved-online",
        _ => "scheduled",
    };

    /// <summary>Names where a source's listing stands, in the page's wire words; null when the source reports no state.</summary>
    public static string? SourceStateName(EventSourceState? state) => state switch
    {
        EventSourceState.Live => "live",
        EventSourceState.Suspect => "suspect",
        EventSourceState.Gone => "gone",
        EventSourceState.Unmatched => "unmatched",
        _ => null,
    };

    /// <summary>Names where the event is attended, in the page's wire words.</summary>
    public static string AttendanceName(EventAttendance attendance) => attendance switch
    {
        EventAttendance.Online => "online",
        EventAttendance.Mixed => "mixed",
        _ => "offline",
    };

    private static bool IsApplication(EventDeadline deadline) => deadline.Kind == DeadlineKind.Application;

    /// <summary>
    /// Picks the title for the caller: the first preferred language the event has a title in (a tag matches by its language
    /// prefix, so <c>uz</c> finds <c>uz-Latn</c>, then <c>uz-Cyrl</c>), then <c>und</c>, <c>en</c>, <c>uz</c>, <c>ru</c>, then
    /// any title. Returns the title with the key it came under.
    /// </summary>
    public static (string Title, string? Language) Title(
        IReadOnlyDictionary<string, string> titles, IReadOnlyList<string> preferred)
    {
        foreach (string tag in preferred.Concat(FallbackTitleLanguages))
            if (TitleFor(titles, tag) is { } found) return found;
        KeyValuePair<string, string> any = titles.FirstOrDefault(pair => !string.IsNullOrWhiteSpace(pair.Value));
        return string.IsNullOrWhiteSpace(any.Value) ? ("(untitled)", null) : (any.Value, any.Key);
    }

    /// <summary>Finds the title under <paramref name="tag"/>: the exact key, else the bare language, else its Latin script,
    /// else any key with the same language prefix in key order.</summary>
    private static (string Title, string Language)? TitleFor(IReadOnlyDictionary<string, string> titles, string tag)
    {
        string language = LanguageOf(tag);
        if (language.Length == 0) return null;
        var candidates = titles
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Value) && LanguageOf(pair.Key) == language)
            .OrderBy(pair => string.Equals(pair.Key, tag, StringComparison.OrdinalIgnoreCase) ? 0
                : string.Equals(pair.Key, language, StringComparison.OrdinalIgnoreCase) ? 1
                : pair.Key.EndsWith("-Latn", StringComparison.OrdinalIgnoreCase) ? 2 : 3)
            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .ToList();
        return candidates.Count == 0 ? null : (candidates[0].Value, candidates[0].Key);
    }

    private static string LanguageOf(string tag)
    {
        int dash = tag.IndexOf('-', StringComparison.Ordinal);
        return (dash < 0 ? tag : tag[..dash]).Trim().ToLowerInvariant();
    }

    private static TrackingPriority TrackingPriorityOf(string priority) => priority switch
    {
        "high" => TrackingPriority.High,
        "low" => TrackingPriority.Low,
        _ => TrackingPriority.Normal,
    };

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
