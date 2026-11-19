using WoW.Two.Sdk.Beta.Be.Events;
using WoW.Two.Sdk.Beta.Be.Events.CatalogEvents;
using WoW.Two.Sdk.Beta.Be.Foundation.Errors;

namespace OcharoEvents.Api.Events.Models;

/// <summary>Holds the event filters of the calendar export as the <c>fe-events</c> client names them in the query, which
/// are WoW2 Events' own (<see cref="EventQuery"/>'s properties in camelCase).</summary>
public sealed class EventQueryParameters
{
    /// <summary>Gets or sets the first day, <c>yyyy-MM-dd</c>.</summary>
    public string? From { get; set; }

    /// <summary>Gets or sets the last day, <c>yyyy-MM-dd</c>.</summary>
    public string? To { get; set; }

    /// <summary>Gets or sets the kind's key.</summary>
    public string? Kind { get; set; }

    /// <summary>Gets or sets the country code.</summary>
    public string? Country { get; set; }

    /// <summary>Gets or sets the city.</summary>
    public string? City { get; set; }

    /// <summary>Gets or sets the free text.</summary>
    public string? Text { get; set; }

    /// <summary>Gets or sets the event status's wire name, such as <c>movedOnline</c>.</summary>
    public string? Status { get; set; }

    /// <summary>Gets or sets the review state's wire name, such as <c>published</c>.</summary>
    public string? Review { get; set; }

    /// <summary>Gets or sets the day an open deadline falls on or before, <c>yyyy-MM-dd</c>.</summary>
    public string? DeadlineBefore { get; set; }

    /// <summary>Gets or sets whether undated events list; true when absent.</summary>
    public bool? IncludeUndated { get; set; }

    /// <summary>Maps the parameters onto Events' query; the export reads every page, so no cursor or size crosses.</summary>
    /// <param name="query">The query; empty when the parameters are malformed.</param>
    /// <returns>The validation error; none when the parameters map.</returns>
    public AppError? TryMap(out EventQuery query)
    {
        query = new EventQuery();
        AppError?[] errors =
        [
            EventsQueryValues.TryDay("from", From, out var from),
            EventsQueryValues.TryDay("to", To, out var to),
            EventsQueryValues.TryDay("deadlineBefore", DeadlineBefore, out var deadlineBefore),
            EventsQueryValues.TryEnum<EventStatus>("status", Status, out var status),
            EventsQueryValues.TryEnum<EventReviewState>("review", Review, out var review),
        ];
        if (errors.FirstOrDefault(error => error is not null) is { } invalid)
            return invalid;

        query = new EventQuery
        {
            From = from,
            To = to,
            Kind = Kind,
            Country = Country,
            City = City,
            Text = Text,
            Status = status,
            Review = review,
            DeadlineBefore = deadlineBefore,
            IncludeUndated = IncludeUndated ?? true,
            Limit = EventsConstants.DefaultPageSize,
        };
        return null;
    }
}
