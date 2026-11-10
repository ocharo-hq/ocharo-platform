namespace OcharoEvents.Infrastructure.Events.Services;

/// <summary>Holds which WoW2 Events instance and catalog serve the Events page (section <c>Events</c>).</summary>
public sealed class EventsServiceOptions
{
    public const string SectionName = "Events";

    /// <summary>
    /// Gets or sets the WoW2 Events root URL. A deployed run refuses to start without it; a local run without it answers
    /// 503 on the Events routes. This service keeps no events store of its own (events-app.md #2), so there is no
    /// fallback.
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>Gets or sets the catalog slug; Ocharo's intake and import write <c>ocharo-fashion</c>.</summary>
    public string Catalog { get; set; } = "ocharo-fashion";
}
