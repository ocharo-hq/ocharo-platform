using Microsoft.Net.Http.Headers;
using OcharoEvents.Application.Events.Services;

namespace OcharoEvents.Api.Events;

/// <summary>Reads the interface language from the request's <c>Accept-Language</c> header, which the Events app sets from the
/// operator's language choice (E25); a missing or malformed header names no preference.</summary>
public sealed class HttpUiLocale(IHttpContextAccessor accessor) : IUiLocale
{
    /// <summary>Holds the most tags one request names, a guard against an oversized header.</summary>
    private const int MaximumTags = 8;

    /// <inheritdoc />
    public IReadOnlyList<string> Preferred => Parse(accessor.HttpContext?.Request.Headers.AcceptLanguage.ToString());

    /// <summary>Lists the header's language tags by descending weight, dropping the wildcard and tags weighted zero.</summary>
    public static IReadOnlyList<string> Parse(string? header)
    {
        if (string.IsNullOrWhiteSpace(header)
            || !StringWithQualityHeaderValue.TryParseList(header.Split(','), out var values))
            return [];
        return values
            .Where(value => value.Value.HasValue && value.Value.Value != "*" && value.Quality is null or > 0)
            .OrderByDescending(value => value.Quality ?? 1.0)
            .Select(value => value.Value.Value!)
            .Take(MaximumTags)
            .ToList();
    }
}
