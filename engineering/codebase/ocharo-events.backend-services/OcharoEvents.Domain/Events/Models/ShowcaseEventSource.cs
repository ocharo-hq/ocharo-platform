namespace OcharoEvents.Domain.Events.Models;

/// <summary>One source that reported the event: its name as people know it, the page it was found on, and where its
/// listing stands as the source last reported it.</summary>
/// <param name="Name">The source's name, such as <c>Afisha</c>; null for an import or a curator's entry.</param>
/// <param name="Url">The page the event was found on.</param>
/// <param name="State">The listing's state: <c>live</c>, <c>suspect</c>, <c>gone</c> or <c>unmatched</c>; null when the
/// source reports none.</param>
public sealed record ShowcaseEventSource(string? Name, string? Url, string? State);
