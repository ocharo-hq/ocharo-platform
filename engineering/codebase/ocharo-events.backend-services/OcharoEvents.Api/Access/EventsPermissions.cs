namespace OcharoEvents.Api.Access;

/// <summary>The organization permissions of client 11's <c>events</c> namespace this host checks (events-app.md #4, #5).</summary>
public static class EventsPermissions
{
    /// <summary>Reads the organization's events: every route needs it, or the organization answers 404.</summary>
    public const string Read = "events:read";

    /// <summary>Proposes events and saves tracking; WoW2 Events checks it again on the exchanged token.</summary>
    public const string Track = "events:track";
}
