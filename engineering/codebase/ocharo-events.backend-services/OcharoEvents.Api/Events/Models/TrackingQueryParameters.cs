using WoW.Two.Sdk.Beta.Be.Events;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Events.Tracking;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;

namespace OcharoEvents.Api.Events.Models;

/// <summary>Holds the tracking list's filters as the <c>fe-events</c> client names them in the query, which are WoW2
/// Events' own (<see cref="TrackingQuery"/>'s properties in camelCase; lists as repeated keys, such as
/// <c>excludeStages=skipped&amp;excludeStages=rejected</c>); the tracking facets read takes the same query.</summary>
public sealed class TrackingQueryParameters
{
    /// <summary>Gets or sets whether only tracked events list.</summary>
    public bool? TrackedOnly { get; set; }

    /// <summary>Gets or sets the stage's key.</summary>
    public string? Stage { get; set; }

    /// <summary>Gets or sets the stages to list.</summary>
    public string[]? Stages { get; set; }

    /// <summary>Gets or sets the stages to leave out.</summary>
    public string[]? ExcludeStages { get; set; }

    /// <summary>Gets or sets the priority's wire name, such as <c>high</c>.</summary>
    public string? Priority { get; set; }

    /// <summary>Gets or sets the first day, <c>yyyy-MM-dd</c>.</summary>
    public string? From { get; set; }

    /// <summary>Gets or sets the last day an event may start on, <c>yyyy-MM-dd</c>.</summary>
    public string? To { get; set; }

    /// <summary>Gets or sets the kind's key.</summary>
    public string? Kind { get; set; }

    /// <summary>Gets or sets the kinds to list.</summary>
    public string[]? Kinds { get; set; }

    /// <summary>Gets or sets the city, matched without case.</summary>
    public string? City { get; set; }

    /// <summary>Gets or sets the cities to list.</summary>
    public string[]? Cities { get; set; }

    /// <summary>Gets or sets the event status's wire name, such as <c>movedOnline</c>.</summary>
    public string? Status { get; set; }

    /// <summary>Gets or sets the source names to list: an intake source's name, or <c>curated</c> for the events no intake
    /// source reported.</summary>
    public string[]? Sources { get; set; }

    /// <summary>Gets or sets the first instant an event may have been published at, ISO 8601 such as
    /// <c>2026-11-19T00:00:00Z</c>.</summary>
    public string? PublishedSince { get; set; }

    /// <summary>Gets or sets the country code.</summary>
    public string? Country { get; set; }

    /// <summary>Gets or sets the countries to list.</summary>
    public string[]? Countries { get; set; }

    /// <summary>Gets or sets the countries to leave out.</summary>
    public string[]? ExcludeCountries { get; set; }

    /// <summary>Gets or sets whether an event with no country matches a country filter; false when absent.</summary>
    public bool? IncludeUnknownCountry { get; set; }

    /// <summary>Gets or sets the free text.</summary>
    public string? Text { get; set; }

    /// <summary>Gets or sets the day an open deadline falls on or before, <c>yyyy-MM-dd</c>.</summary>
    public string? DeadlineBefore { get; set; }

    /// <summary>Gets or sets whether undated events list; true when absent.</summary>
    public bool? IncludeUndated { get; set; }

    /// <summary>Gets or sets the order's wire name: <c>starts</c> (the default), <c>added</c> or <c>deadline</c>; a cursor
    /// holds for the order it came from.</summary>
    public string? Sort { get; set; }

    /// <summary>Gets or sets the page's cursor.</summary>
    public string? Cursor { get; set; }

    /// <summary>Gets or sets the page's size.</summary>
    public int? Limit { get; set; }

    /// <summary>Maps the parameters onto Events' query; Events validates the lists, the cursor and the size itself.</summary>
    /// <param name="query">The query; empty when the parameters are malformed.</param>
    /// <returns>The validation error; none when the parameters map.</returns>
    public AppError? TryMap(out TrackingQuery query)
    {
        query = new TrackingQuery();
        AppError?[] errors =
        [
            EventsQueryValues.TryDay("from", From, out var from),
            EventsQueryValues.TryDay("to", To, out var to),
            EventsQueryValues.TryDay("deadlineBefore", DeadlineBefore, out var deadlineBefore),
            EventsQueryValues.TryEnum<TrackingPriority>("priority", Priority, out var priority),
            EventsQueryValues.TryEnum<EventStatus>("status", Status, out var status),
            EventsQueryValues.TryEnum<TrackingSort>("sort", Sort, out var sort),
            EventsQueryValues.TryInstant("publishedSince", PublishedSince, out var publishedSince),
        ];
        if (errors.FirstOrDefault(error => error is not null) is { } invalid)
            return invalid;

        query = new TrackingQuery
        {
            TrackedOnly = TrackedOnly ?? false,
            Stage = Stage,
            Stages = Stages ?? [],
            ExcludeStages = ExcludeStages ?? [],
            Priority = priority,
            From = from,
            To = to,
            Kind = Kind,
            Kinds = Kinds ?? [],
            City = City,
            Cities = Cities ?? [],
            Status = status,
            Sources = Sources ?? [],
            PublishedSince = publishedSince,
            Country = Country,
            Countries = Countries ?? [],
            ExcludeCountries = ExcludeCountries ?? [],
            IncludeUnknownCountry = IncludeUnknownCountry ?? false,
            Text = Text,
            DeadlineBefore = deadlineBefore,
            IncludeUndated = IncludeUndated ?? true,
            Sort = sort ?? TrackingSort.Starts,
            Cursor = Cursor,
            Limit = Limit ?? EventsConstants.DefaultPageSize,
        };
        return null;
    }
}
