using System.Text.RegularExpressions;
using BrandWorkspace.Application.Preparation.Models;
using BrandWorkspace.Application.Sourcing.Models;

namespace BrandWorkspace.Infrastructure.Sourcing.Services;

/// <summary>Normalizes and bounds sourcing fields before they reach organization-scoped persistence.</summary>
public sealed partial class SourcingValidationService
{
    public static readonly IReadOnlyList<string> Stages = ["lead", "contacted", "replied", "quoted", "qualified", "dropped"];
    public static readonly IReadOnlyList<string> Channels =
        ["whatsapp", "email", "phone", "form", "website", "instagram", "facebook", "telegram", "marketplace", "other"];
    public static readonly IReadOnlyList<string> Captures = ["original", "screenshot", "derived"];
    public const int MaximumAssignedMedia = 200;

    public SupplierInput Supplier(string? name, string? stage, string? country, string? city, string? website, string? notes)
    {
        return new SupplierInput(
            Required(name, "Supplier name", 160),
            Stage(stage ?? "lead"),
            Country(country),
            Optional(city, "City", 120),
            Url(website, "Website"),
            Text(notes, "Notes", 8000));
    }

    public PersonInput Person(string? name, string? role, string? notes)
    {
        return new PersonInput(Required(name, "Person name", 160), Optional(role, "Role", 120), Text(notes, "Notes", 4000));
    }

    public RouteInput Route(
        string? channel, string? destination, string? label, string? sourceUrl, bool verified, bool isPrimary, Guid? personId,
        string? notes)
    {
        string validatedChannel = Channel(channel);
        return new RouteInput(
            validatedChannel,
            Destination(validatedChannel, destination),
            Optional(label, "Label", 120),
            Url(sourceUrl, "Source link"),
            verified,
            isPrimary,
            personId,
            Text(notes, "Notes", 4000));
    }

    public MediaUpdateInput Media(Guid? supplierId, string? capture, string? caption)
    {
        return new MediaUpdateInput(supplierId, Capture(capture), Text(caption, "Caption", 2000));
    }

    public string Stage(string? value)
    {
        string stage = value?.Trim() ?? "";
        return Stages.Contains(stage) ? stage : throw Invalid("Choose a supplier stage: " + string.Join(", ", Stages) + ".");
    }

    public string? StageFilter(string? value) => string.IsNullOrWhiteSpace(value) ? null : Stage(value);

    public string Channel(string? value)
    {
        string channel = value?.Trim() ?? "";
        return Channels.Contains(channel) ? channel : throw Invalid("Choose a contact channel: " + string.Join(", ", Channels) + ".");
    }

    public string Capture(string? value)
    {
        string capture = string.IsNullOrWhiteSpace(value) ? "original" : value.Trim();
        return Captures.Contains(capture) ? capture : throw Invalid("Capture must be original, screenshot or derived.");
    }

    public string Caption(string? value) => Text(value, "Caption", 2000);

    public string? Query(string? value)
    {
        string? query = Optional(value, "Search", 120);
        return query;
    }

    public string? Key(string? value)
    {
        if (value is null) return null;
        return KeyPattern().IsMatch(value) ? value
            : throw Invalid("Import keys use 1–64 lowercase letters, digits and hyphens.");
    }

    public string? SourceLabel(string? value) => Optional(value, "Source label", 200);

    public string? SourceMessageId(string? value) => Optional(value, "Source message", 200);

    /// <summary>Keeps only the final file name segment and replaces characters that are unsafe in headers or paths.</summary>
    public string OriginalName(string? value)
    {
        string name = (value ?? "").Replace('\\', '/');
        name = name[(name.LastIndexOf('/') + 1)..].Trim();
        name = new string(name.Select(character => char.IsControl(character) || character is ':' or '"' ? '_' : character).ToArray());
        if (name is "" or "." or "..") name = "upload";
        return name.Length <= 255 ? name : name[..255];
    }

    public void Page(int limit, int offset)
    {
        if (limit is < 1 or > 200 || offset is < 0 or > 1000000)
            throw Invalid("Limit must be 1 to 200 and offset must be 0 to 1000000.");
    }

    public void Revision(int revision)
    {
        if (revision is < 1 or int.MaxValue) throw Invalid("Provide a valid expected revision.");
    }

    public IReadOnlyList<Guid> MediaIds(IReadOnlyList<Guid>? ids)
    {
        if (ids is null || ids.Count is < 1 or > MaximumAssignedMedia)
            throw Invalid($"Select 1 to {MaximumAssignedMedia} media items.");
        return ids.Distinct().ToArray();
    }

    private string Destination(string channel, string? value)
    {
        string destination = Required(value, "Destination", 512);
        switch (channel)
        {
            case "whatsapp" or "phone":
                string number = PhoneSeparators().Replace(destination, "");
                return PhonePattern().IsMatch(number) ? number
                    : throw Invalid("Phone and WhatsApp destinations need 6 to 20 digits, optionally starting with +.");
            case "email":
                string email = destination.ToLowerInvariant();
                return email.Length <= 254 && EmailPattern().IsMatch(email) ? email : throw Invalid("Enter a valid email address.");
            case "website" or "form" or "marketplace":
                return Url(destination, "Destination")!;
            default:
                return destination;
        }
    }

    private static string? Country(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        string country = value.Trim().ToUpperInvariant();
        return CountryPattern().IsMatch(country) ? country : throw Invalid("Use a two-letter country code such as IN or UZ.");
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

    [GeneratedRegex(@"[\s\-().]")]
    private static partial Regex PhoneSeparators();

    [GeneratedRegex(@"^\+?[0-9]{6,20}$")]
    private static partial Regex PhonePattern();

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailPattern();
}
