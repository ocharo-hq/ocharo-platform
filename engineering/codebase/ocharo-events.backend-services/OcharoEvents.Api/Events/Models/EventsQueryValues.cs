using System.Globalization;
using System.Text.Json;
using OcharoEvents.Application.Events.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;
using WoW.Two.Sdk.Beta.Be.Foundation.Serialization;

namespace OcharoEvents.Api.Events.Models;

/// <summary>Reads mirrored query values as WoW2 Events' own routes read them: enums by their camelCase wire names
/// (<c>high</c>, <c>movedOnline</c>), days as <c>yyyy-MM-dd</c>, instants as ISO 8601 with an offset (<c>2026-11-19T00:00:00Z</c>); an empty value is no filter.</summary>
internal static class EventsQueryValues
{
    /// <summary>Reads an enum's wire name.</summary>
    /// <returns>The validation error for an unknown name; none otherwise.</returns>
    public static AppError? TryEnum<TEnum>(string name, string? value, out TEnum? parsed) where TEnum : struct, Enum
    {
        parsed = null;
        if (string.IsNullOrWhiteSpace(value))
            return null;
        try
        {
            parsed = JsonSerializer.Deserialize<TEnum>(JsonSerializer.Serialize(value), JsonOptionsConstants.Default);
            return null;
        }
        catch (JsonException)
        {
            return OcharoEventsErrors.Invalid($"{name}: '{value}' is not a known value.");
        }
    }

    /// <summary>Reads a day.</summary>
    /// <returns>The validation error for a value that is no <c>yyyy-MM-dd</c> day; none otherwise.</returns>
    public static AppError? TryDay(string name, string? value, out DateOnly? parsed)
    {
        parsed = null;
        if (string.IsNullOrWhiteSpace(value))
            return null;
        if (DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
        {
            parsed = day;
            return null;
        }
        return OcharoEventsErrors.Invalid($"{name}: use a day such as 2026-12-03.");
    }

    /// <summary>Reads an instant.</summary>
    /// <returns>The validation error for a value that is no ISO 8601 instant with an offset; none otherwise.</returns>
    public static AppError? TryInstant(string name, string? value, out DateTimeOffset? parsed)
    {
        parsed = null;
        if (string.IsNullOrWhiteSpace(value))
            return null;
        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var instant))
        {
            parsed = instant;
            return null;
        }
        return OcharoEventsErrors.Invalid($"{name}: use an instant such as 2026-11-19T00:00:00Z.");
    }
}
