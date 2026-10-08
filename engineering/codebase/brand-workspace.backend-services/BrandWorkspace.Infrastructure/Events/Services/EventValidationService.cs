using System.Globalization;
using System.Text.RegularExpressions;
using BrandWorkspace.Application.Events.Models;
using BrandWorkspace.Application.Preparation.Models;

namespace BrandWorkspace.Infrastructure.Events.Services;

/// <summary>Unvalidated event fields as an operator or an import bundle sent them; dates are ISO <c>YYYY-MM-DD</c> strings.</summary>
public sealed record EventFields(
    string? Title,
    string? Organizer,
    string? Scope,
    string? Format,
    string? Country,
    string? City,
    string? Venue,
    string? StartsOn,
    string? EndsOn,
    string? DatePrecision,
    string? ApplicationDeadline,
    string? Participation,
    string? Cost,
    string? Website,
    string? SourceUrl,
    string? Status,
    string? Priority,
    string? Notes);

/// <summary>Normalizes and bounds event fields before they reach organization-scoped persistence.</summary>
public sealed partial class EventValidationService
{
    public static readonly IReadOnlyList<string> Scopes = ["local", "regional", "international"];
    public static readonly IReadOnlyList<string> Formats =
        ["runway", "trade-fair", "market", "exhibition", "competition", "festival", "conference", "other"];
    public static readonly IReadOnlyList<string> Statuses =
        ["candidate", "shortlisted", "applying", "applied", "confirmed", "attended", "skipped", "rejected"];
    public static readonly IReadOnlyList<string> DatePrecisions = ["day", "month", "estimated", "unknown"];
    public static readonly IReadOnlyList<string> Priorities = ["high", "normal", "low"];
    public static readonly DateOnly EarliestDate = new(2000, 1, 1);
    public static readonly DateOnly LatestDate = new(2100, 12, 31);
    public const string DefaultStatus = "shortlisted";
    public const string ImportedStatus = "candidate";
    public const int MaximumLimit = 500;

    /// <summary>Validates every field; a blank status falls back to <paramref name="defaultStatus"/>.</summary>
    public ShowcaseEventInput Event(EventFields fields, string defaultStatus = DefaultStatus)
    {
        string title = Required(fields.Title, "Title", 200);
        string? organizer = Optional(fields.Organizer, "Organizer", 160);
        string scope = Scope(fields.Scope);
        string format = Format(fields.Format);
        string? country = Country(fields.Country);
        string? city = Optional(fields.City, "City", 120);
        string? venue = Optional(fields.Venue, "Venue", 200);
        DateOnly? startsOn = Date(fields.StartsOn, "Start date");
        DateOnly? endsOn = Date(fields.EndsOn, "End date");
        string precision = string.IsNullOrWhiteSpace(fields.DatePrecision)
            ? startsOn is null ? "unknown" : "day"
            : DatePrecision(fields.DatePrecision);
        Dates(precision, startsOn, endsOn);
        DateOnly? deadline = Date(fields.ApplicationDeadline, "Application deadline");
        string participation = Text(fields.Participation, "Participation", 2000);
        string? cost = OptionalText(fields.Cost, "Cost", 500);
        string? website = Url(fields.Website, "Website");
        string? sourceUrl = Url(fields.SourceUrl, "Source link");
        string status = Status(string.IsNullOrWhiteSpace(fields.Status) ? defaultStatus : fields.Status);
        string priority = Priority(string.IsNullOrWhiteSpace(fields.Priority) ? "normal" : fields.Priority);
        string notes = Text(fields.Notes, "Notes", 8000);
        return new ShowcaseEventInput(
            title, organizer, scope, format, country, city, venue, startsOn, endsOn, precision, deadline, participation, cost,
            website, sourceUrl, status, priority, notes);
    }

    public ShowcaseEventFilter Filter(
        string? query, string? scope, string? format, string? status, string? from, string? to, bool includeUndated)
    {
        return new ShowcaseEventFilter(
            Optional(query, "Search", 120),
            string.IsNullOrWhiteSpace(scope) ? null : Scope(scope),
            string.IsNullOrWhiteSpace(format) ? null : Format(format),
            string.IsNullOrWhiteSpace(status) ? null : Status(status),
            Date(from, "From date"),
            Date(to, "To date"),
            includeUndated);
    }

    public string Scope(string? value)
    {
        string scope = value?.Trim() ?? "";
        return Scopes.Contains(scope) ? scope : throw Invalid("Choose a scope: local, regional or international.");
    }

    public string Format(string? value)
    {
        string format = value?.Trim() ?? "";
        return Formats.Contains(format) ? format : throw Invalid("Choose an event format: " + string.Join(", ", Formats) + ".");
    }

    public string Status(string? value)
    {
        string status = value?.Trim() ?? "";
        return Statuses.Contains(status) ? status : throw Invalid("Choose an event status: " + string.Join(", ", Statuses) + ".");
    }

    public string DatePrecision(string? value)
    {
        string precision = value?.Trim() ?? "";
        return DatePrecisions.Contains(precision) ? precision
            : throw Invalid("Choose a date precision: day, month, estimated or unknown.");
    }

    public string Priority(string? value)
    {
        string priority = value?.Trim() ?? "";
        return Priorities.Contains(priority) ? priority : throw Invalid("Choose a priority: high, normal or low.");
    }

    /// <summary>Parses an optional ISO date for the summary; null means the caller uses today's UTC date.</summary>
    public DateOnly? Today(string? value) => Date(value, "Today");

    public string? Key(string? value)
    {
        if (value is null) return null;
        return KeyPattern().IsMatch(value) ? value
            : throw Invalid("Import keys use 1–64 lowercase letters, digits and hyphens.");
    }

    public void Page(int limit, int offset)
    {
        if (limit is < 1 or > MaximumLimit || offset is < 0 or > 1000000)
            throw Invalid($"Limit must be 1 to {MaximumLimit} and offset must be 0 to 1000000.");
    }

    public void Revision(int revision)
    {
        if (revision is < 1 or int.MaxValue) throw Invalid("Provide a valid expected revision.");
    }

    private static void Dates(string precision, DateOnly? startsOn, DateOnly? endsOn)
    {
        if (endsOn is not null && startsOn is null) throw Invalid("An end date needs a start date.");
        if (endsOn < startsOn) throw Invalid("The end date must be on or after the start date.");
        if (precision == "unknown")
        {
            if (startsOn is not null) throw Invalid("Leave the dates empty when the date precision is unknown.");
            return;
        }
        if (startsOn is null) throw Invalid("A start date is required unless the date precision is unknown.");
        if (precision is "month" or "estimated" && startsOn.Value.Day != 1)
            throw Invalid("For month or estimated precision, the start date must be the 1st of the month.");
    }

    private static DateOnly? Date(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (!DateOnly.TryParseExact(value.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly date))
            throw Invalid($"{field} must be a date such as 2026-10-08.");
        if (date < EarliestDate || date > LatestDate) throw Invalid($"{field} must be between 2000-01-01 and 2100-12-31.");
        return date;
    }

    private static string? Country(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        string country = value.Trim().ToUpperInvariant();
        return CountryPattern().IsMatch(country) ? country : throw Invalid("Use a two-letter country code such as UZ or FR.");
    }

    private static string? Url(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        string url = value.Trim();
        if (url.Length > 2048 || !Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
            || uri.Scheme is not ("http" or "https") || string.IsNullOrEmpty(uri.Host))
            throw Invalid($"{field} must be an http or https address.");
        return url;
    }

    private static string Required(string? value, string field, int maximum)
    {
        return Optional(value, field, maximum) ?? throw Invalid($"{field} is required.");
    }

    private static string? Optional(string? value, string field, int maximum)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        string text = value.Trim();
        if (text.Length > maximum) throw Invalid($"{field} must be at most {maximum} characters.");
        if (text.Any(char.IsControl)) throw Invalid($"{field} must be a single line of text.");
        return text;
    }

    private static string? OptionalText(string? value, string field, int maximum)
    {
        string text = Text(value, field, maximum);
        return text.Length == 0 ? null : text;
    }

    private static string Text(string? value, string field, int maximum)
    {
        string text = (value ?? "").Trim();
        if (text.Length > maximum) throw Invalid($"{field} must be at most {maximum} characters.");
        if (text.Any(character => char.IsControl(character) && character is not ('\n' or '\r' or '\t')))
            throw Invalid($"{field} contains unsupported control characters.");
        return text;
    }

    private static PreparationException Invalid(string message) => new("validation", message);

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,63}$")]
    private static partial Regex KeyPattern();

    [GeneratedRegex("^[A-Z]{2}$")]
    private static partial Regex CountryPattern();
}
